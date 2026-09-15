using System;
using System.Collections.Generic;
using System.Linq;
using CockroachFantasia.Characters;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using CockroachFantasia.UI;
using CockroachFantasia.Audio;

namespace CockroachFantasia.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkGameManager : NetworkBehaviour
    {
        private static NetworkGameManager instance;

        [SerializeField] private MatchRules rules;
        private NetworkVariable<MatchPhase> phase;
        private NetworkVariable<MatchWinner> winner;
        private NetworkVariable<int> depositedPoints;
        private NetworkVariable<double> playingEndTimestamp;
        private NetworkList<ulong> resultAcknowledgements;
        private MatchStateMachine stateMachine;

        public static NetworkGameManager Instance => instance;
        public MatchPhase Phase => phase.Value;
        public MatchWinner Winner => winner.Value;
        public int DepositedPoints => depositedPoints.Value;
        public double PlayingEndTimestamp => playingEndTimestamp.Value;
        public MatchRules Rules => rules;
        public bool AcceptsGameplayRequests => phase.Value == MatchPhase.Playing;
        public bool AllClientsAcknowledgedResults => IsServer && NetworkManager != null &&
            NetworkManager.ConnectedClientsIds.Count > 0 &&
            NetworkManager.ConnectedClientsIds.All(clientId => resultAcknowledgements.Contains(clientId));
        public double RemainingPlayingSeconds => phase.Value == MatchPhase.Playing && NetworkManager != null
            ? MatchClock.RemainingSeconds(playingEndTimestamp.Value, NetworkManager.ServerTime.Time)
            : 0d;

        public event Action StateChanged;

        public void Configure(MatchRules matchRules)
        {
            rules = matchRules;
        }

        private void Awake()
        {
            phase ??= new NetworkVariable<MatchPhase>();
            winner ??= new NetworkVariable<MatchWinner>();
            depositedPoints ??= new NetworkVariable<int>();
            playingEndTimestamp ??= new NetworkVariable<double>();
            resultAcknowledgements ??= new NetworkList<ulong>();
        }

        public override void OnNetworkSpawn()
        {
            instance = this;
            phase.OnValueChanged += OnPhaseChanged;
            winner.OnValueChanged += OnWinnerChanged;
            depositedPoints.OnValueChanged += OnScoreChanged;
            playingEndTimestamp.OnValueChanged += OnDeadlineChanged;
            resultAcknowledgements.OnListChanged += OnResultAcknowledgementChanged;
            ApplyPlayerControl();
            if (phase.Value == MatchPhase.Results) AcknowledgeResultsRpc();
            if (!IsServer) return;
            if (rules == null)
            {
                Debug.LogError("NetworkGameManager has no MatchRules asset.");
                return;
            }

            stateMachine = new MatchStateMachine(rules.MatchDurationSeconds, rules.FoodQuotaPoints,
                rules.CountdownSeconds);
            NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            PublishState();
        }

        public override void OnNetworkDespawn()
        {
            phase.OnValueChanged -= OnPhaseChanged;
            winner.OnValueChanged -= OnWinnerChanged;
            depositedPoints.OnValueChanged -= OnScoreChanged;
            playingEndTimestamp.OnValueChanged -= OnDeadlineChanged;
            resultAcknowledgements.OnListChanged -= OnResultAcknowledgementChanged;
            if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || stateMachine == null) return;
            var previousPhase = stateMachine.Phase;
            var previousWinner = stateMachine.Winner;
            stateMachine.Tick(NetworkManager.ServerTime.Time);
            if (stateMachine.Phase != previousPhase || stateMachine.Winner != previousWinner)
                PublishState();
        }

        public bool TryDepositPointsByServer(int points)
        {
            if (!IsServer || stateMachine == null) return false;
            var accepted = stateMachine.TryDepositPoints(points, NetworkManager.ServerTime.Time);
            if (accepted) PublishState();
            return accepted;
        }

        public bool FinishForDiagnosticsByServer(MatchWinner diagnosticWinner)
        {
            if (!IsServer || !Debug.isDebugBuild || stateMachine == null ||
                !Array.Exists(Environment.GetCommandLineArgs(), argument =>
                    string.Equals(argument, "-resultsRematchSmoke", StringComparison.OrdinalIgnoreCase)))
                return false;
            var accepted = stateMachine.FinishForDiagnostics(diagnosticWinner);
            if (accepted) PublishState();
            return accepted;
        }

        public void PlayDepositVfxByServer(Vector3 position, int points)
        {
            if (IsServer) PlayDepositVfxRpc(position, points);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayDepositVfxRpc(Vector3 position, int points)
        {
            ComicVfx.SpawnBurst(position + Vector3.up * 0.2f, new Color(0.55f, 1f, 0.38f), $"YUM! +{points}");
            GameAudio.Play(GameAudioCue.Deposit, position);
            GameAudio.Play(GameAudioCue.Score, null, 0.13f);
        }

        private void PublishState()
        {
            if (IsServer && stateMachine.Phase != MatchPhase.Results && resultAcknowledgements.Count > 0)
                resultAcknowledgements.Clear();
            phase.Value = stateMachine.Phase;
            winner.Value = stateMachine.Winner;
            depositedPoints.Value = stateMachine.DepositedPoints;
            playingEndTimestamp.Value = stateMachine.PlayingEndTimestamp;
        }

        private void OnPhaseChanged(MatchPhase previous, MatchPhase current)
        {
            ApplyPlayerControl();
            if (current == MatchPhase.Results) AcknowledgeResultsRpc();
            StateChanged?.Invoke();
        }
        private void OnWinnerChanged(MatchWinner previous, MatchWinner current) => StateChanged?.Invoke();
        private void OnScoreChanged(int previous, int current) => StateChanged?.Invoke();
        private void OnDeadlineChanged(double previous, double current) => StateChanged?.Invoke();
        private void OnResultAcknowledgementChanged(NetworkListEvent<ulong> change) => StateChanged?.Invoke();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void AcknowledgeResultsRpc(RpcParams rpcParams = default)
        {
            if (!IsServer || phase.Value != MatchPhase.Results) return;
            var clientId = rpcParams.Receive.SenderClientId;
            if (!resultAcknowledgements.Contains(clientId)) resultAcknowledgements.Add(clientId);
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            if (sceneName != "Kitchen" || clientsTimedOut.Count > 0 || stateMachine == null) return;
            stateMachine.BeginCountdown(NetworkManager.ServerTime.Time);
            PublishState();
        }

        private void ApplyPlayerControl()
        {
            var playing = phase.Value == MatchPhase.Playing;
            foreach (var motor in FindObjectsByType<CockroachMotor>(FindObjectsSortMode.None))
                motor.SetControlState(playing, motor.IsRespawning);
            foreach (var motor in FindObjectsByType<HumanMotor>(FindObjectsSortMode.None))
                motor.SetControlState(playing, false);
        }
    }
}
