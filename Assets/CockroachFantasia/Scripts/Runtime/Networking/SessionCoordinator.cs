using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CockroachFantasia.App;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    [DefaultExecutionOrder(-800)]
    public sealed class SessionCoordinator : MonoBehaviour
    {
        public const int MaximumPlayers = 4;
        public const string SessionType = "cockroach-fantasia-mvp";

        private static SessionCoordinator instance;
        private CancellationTokenSource lifetimeCancellation;
        private ISession currentSession;

        public static SessionCoordinator Instance => instance;
        public SessionConnectionState State { get; private set; } = SessionConnectionState.Idle;
        public string RoomCode => currentSession?.Code ?? string.Empty;
        public string StatusMessage { get; private set; } = "Not connected.";
        public int PlayerCount => currentSession?.PlayerCount ?? 0;
        public ISession CurrentSession => currentSession;

        public event Action<SessionConnectionState, string> StatusChanged;
        public event Action SessionChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (instance != null || FindFirstObjectByType<SessionCoordinator>() != null)
            {
                return;
            }

            new GameObject(nameof(SessionCoordinator)).AddComponent<SessionCoordinator>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            lifetimeCancellation = new CancellationTokenSource();
            DontDestroyOnLoad(gameObject);
        }

        private async void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            lifetimeCancellation.Cancel();
            if (currentSession != null)
            {
                try
                {
                    await currentSession.LeaveAsync();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Could not leave Session during shutdown: {exception.Message}");
                }
            }

            lifetimeCancellation.Dispose();
            instance = null;
        }

        public async Task<bool> HostPrivateSessionAsync(CancellationToken cancellationToken = default)
        {
            if (!CanBeginConnection())
            {
                return false;
            }

            SetState(SessionConnectionState.Connecting, "Creating private room…");
            try
            {
                await EnsureServicesReadyAsync(cancellationToken);
                var options = new SessionOptions
                {
                    Name = $"Kitchen-{Guid.NewGuid():N}",
                    Type = SessionType,
                    MaxPlayers = MaximumPlayers,
                    IsPrivate = true,
                    IsLocked = false
                }.WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.DTLS })
                 .WithRelayNetwork();

                AttachSession(await MultiplayerService.Instance.CreateSessionAsync(options));
                SetState(SessionConnectionState.Connected, $"Room {RoomCode} ready — share this code with friends.");
                return true;
            }
            catch (Exception exception)
            {
                HandleConnectionFailure("Could not create the room.", exception);
                return false;
            }
        }

        public async Task<bool> JoinPrivateSessionAsync(string roomCode, CancellationToken cancellationToken = default)
        {
            if (!CanBeginConnection())
            {
                return false;
            }

            var normalized = NormalizeRoomCode(roomCode);
            if (normalized.Length == 0)
            {
                SetState(SessionConnectionState.Failed, "Enter a room code.");
                return false;
            }

            SetState(SessionConnectionState.Connecting, $"Joining room {normalized}…");
            try
            {
                await EnsureServicesReadyAsync(cancellationToken);
                var joinOptions = new JoinSessionOptions { Type = SessionType }
                    .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.DTLS });
                AttachSession(await MultiplayerService.Instance.JoinSessionByCodeAsync(normalized, joinOptions));
                SetState(SessionConnectionState.Connected, $"Joined room {RoomCode}.");
                return true;
            }
            catch (Exception exception)
            {
                HandleConnectionFailure("Could not join that room. Check the code and retry.", exception);
                return false;
            }
        }

        public async Task LeaveAsync()
        {
            if (currentSession == null)
            {
                SetState(SessionConnectionState.Idle, "Not connected.");
                return;
            }

            SetState(SessionConnectionState.Leaving, "Leaving room…");
            var leaving = currentSession;
            DetachSession();

            try
            {
                await leaving.LeaveAsync();
                SetState(SessionConnectionState.Idle, "Left the room.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Session leave failed: {exception.Message}");
                SetState(SessionConnectionState.Failed, "The room closed, but cleanup did not finish cleanly.");
            }
        }

        public void CopyRoomCode()
        {
            if (!string.IsNullOrEmpty(RoomCode))
            {
                GUIUtility.systemCopyBuffer = RoomCode;
                SetState(State, $"Copied room code {RoomCode}.");
            }
        }

        public static string NormalizeRoomCode(string roomCode)
        {
            return string.IsNullOrWhiteSpace(roomCode)
                ? string.Empty
                : new string(roomCode.Where(character => !char.IsWhiteSpace(character) && character != '-').ToArray())
                    .ToUpperInvariant();
        }

        private bool CanBeginConnection()
        {
            if (State == SessionConnectionState.Connecting || State == SessionConnectionState.Leaving)
            {
                return false;
            }

            if (currentSession != null)
            {
                SetState(SessionConnectionState.Failed, "Leave the current room before connecting again.");
                return false;
            }

            return true;
        }

        private async Task EnsureServicesReadyAsync(CancellationToken cancellationToken)
        {
            var bootstrap = ServicesBootstrap.Instance;
            if (bootstrap == null)
            {
                throw new InvalidOperationException("Unity Services bootstrap is unavailable.");
            }

            var result = await bootstrap.InitializeAsync(cancellationToken);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Message);
            }
        }

        private void AttachSession(ISession session)
        {
            currentSession = session ?? throw new ArgumentNullException(nameof(session));
            currentSession.Changed += OnSessionChanged;
            currentSession.RemovedFromSession += OnRemovedFromSession;
            currentSession.Deleted += OnRemovedFromSession;
            OnSessionChanged();
        }

        private void DetachSession()
        {
            if (currentSession == null)
            {
                return;
            }

            currentSession.Changed -= OnSessionChanged;
            currentSession.RemovedFromSession -= OnRemovedFromSession;
            currentSession.Deleted -= OnRemovedFromSession;
            currentSession = null;
            SessionChanged?.Invoke();
        }

        private void OnSessionChanged()
        {
            SessionChanged?.Invoke();
        }

        private void OnRemovedFromSession()
        {
            DetachSession();
            SetState(SessionConnectionState.Idle, "The room was closed.");
        }

        private void HandleConnectionFailure(string friendlyMessage, Exception exception)
        {
            Debug.LogWarning($"Session connection failed: {exception}");
            DetachSession();
            SetState(SessionConnectionState.Failed, friendlyMessage);
        }

        private void SetState(SessionConnectionState state, string message)
        {
            State = state;
            StatusMessage = message;
            StatusChanged?.Invoke(state, message);
        }
    }
}
