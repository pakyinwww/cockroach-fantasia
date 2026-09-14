using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CockroachFantasia.App;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CockroachFantasia.Networking
{
    [DefaultExecutionOrder(-800)]
    public sealed class SessionCoordinator : MonoBehaviour
    {
        public const int MaximumPlayers = 4;
        public const string SessionType = "cockroach-fantasia-mvp";

        private static SessionCoordinator instance;
        private CancellationTokenSource lifetimeCancellation;
        private IPrivateSession currentSession;
        private IPrivateSessionGateway sessionGateway;
        private bool voluntaryLeave;
        private bool handlingTerminalDisconnect;

        public static SessionCoordinator Instance => instance;
        public SessionConnectionState State { get; private set; } = SessionConnectionState.Idle;
        public string RoomCode => currentSession?.Code ?? string.Empty;
        public string StatusMessage { get; private set; } = "Not connected.";
        public SessionFailureKind LastFailureKind { get; private set; } = SessionFailureKind.None;
        public int PlayerCount => currentSession?.PlayerCount ?? 0;
        public IPrivateSession CurrentSession => currentSession;

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
            sessionGateway ??= new UnityPrivateSessionGateway();
            lifetimeCancellation = new CancellationTokenSource();
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            lifetimeCancellation.Cancel();
            DetachSession();
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
                AttachSession(await sessionGateway.CreateAsync(MaximumPlayers, SessionType, cancellationToken));
                SetState(SessionConnectionState.Connected, $"Room {RoomCode} ready — share this code with friends.");
                return true;
            }
            catch (Exception exception)
            {
                HandleConnectionFailure(exception);
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
            if (!IsValidRoomCode(normalized))
            {
                SetFailure(SessionFailureKind.InvalidCode);
                return false;
            }

            SetState(SessionConnectionState.Connecting, $"Joining room {normalized}…");
            try
            {
                await EnsureServicesReadyAsync(cancellationToken);
                AttachSession(await sessionGateway.JoinAsync(normalized, SessionType, cancellationToken));
                SetState(SessionConnectionState.Connected, $"Joined room {RoomCode}.");
                return true;
            }
            catch (Exception exception)
            {
                HandleConnectionFailure(exception);
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
            voluntaryLeave = true;

            try
            {
                await leaving.LeaveAsync();
                DetachSession();
                EnsureNetworkStopped();
                SetState(SessionConnectionState.Idle, "Left the room.");
                LoadFrontEndIfNeeded();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Session leave failed: {exception.Message}");
                DetachSession();
                EnsureNetworkStopped();
                SetFailure(SessionFailureKind.Disconnected, "The room closed, but cleanup did not finish cleanly.");
                LoadFrontEndIfNeeded();
            }
            finally
            {
                voluntaryLeave = false;
            }
        }

        public async Task<bool> SetSessionLockedAsync(bool locked)
        {
            if (currentSession == null || !currentSession.IsHost)
            {
                return false;
            }

            try
            {
                await currentSession.SetLockedAsync(locked);
                SetState(State, locked ? "Room locked for the match." : "Room reopened for players.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not {(locked ? "lock" : "unlock")} Session: {exception}");
                SetFailure(SessionFailureMapper.Map(exception));
                return false;
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

        public void ConfigureSessionGatewayForTests(IPrivateSessionGateway gateway)
        {
            sessionGateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public static string NormalizeRoomCode(string roomCode)
        {
            return string.IsNullOrWhiteSpace(roomCode)
                ? string.Empty
                : new string(roomCode.Where(character => !char.IsWhiteSpace(character) && character != '-').ToArray())
                    .ToUpperInvariant();
        }

        public static bool IsValidRoomCode(string normalizedCode)
        {
            const string validCharacters = "6789BCDFGHJKLMNPQRTW";
            return normalizedCode is { Length: >= 6 and <= 12 } &&
                   normalizedCode.All(character => validCharacters.IndexOf(character) >= 0);
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

        private void AttachSession(IPrivateSession session)
        {
            currentSession = session ?? throw new ArgumentNullException(nameof(session));
            currentSession.Changed += OnSessionChanged;
            currentSession.Removed += OnRemovedFromSession;
            currentSession.Deleted += OnRemovedFromSession;
            currentSession.StateChanged += OnSessionStateChanged;
            SubscribeNetworkCallbacks();
            LastFailureKind = SessionFailureKind.None;
            OnSessionChanged();
        }

        private void DetachSession()
        {
            if (currentSession == null)
            {
                return;
            }

            currentSession.Changed -= OnSessionChanged;
            currentSession.Removed -= OnRemovedFromSession;
            currentSession.Deleted -= OnRemovedFromSession;
            currentSession.StateChanged -= OnSessionStateChanged;
            UnsubscribeNetworkCallbacks();
            currentSession = null;
            SessionChanged?.Invoke();
        }

        private void OnSessionChanged()
        {
            SessionChanged?.Invoke();
        }

        private void OnRemovedFromSession()
        {
            if (!voluntaryLeave)
            {
                HandleTerminalDisconnect(currentSession != null && !currentSession.IsHost
                    ? SessionFailureKind.HostLeft
                    : SessionFailureKind.Disconnected);
            }
        }

        private void OnSessionStateChanged(PrivateSessionState sessionState)
        {
            if (!voluntaryLeave && (sessionState == PrivateSessionState.Deleted || sessionState == PrivateSessionState.Disconnected))
            {
                HandleTerminalDisconnect(currentSession != null && !currentSession.IsHost
                    ? SessionFailureKind.HostLeft
                    : SessionFailureKind.Disconnected);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (voluntaryLeave || currentSession == null || currentSession.IsHost || manager == null ||
                clientId != manager.LocalClientId)
            {
                SessionChanged?.Invoke();
                return;
            }

            HandleTerminalDisconnect(SessionFailureKind.HostLeft);
        }

        private async void HandleTerminalDisconnect(SessionFailureKind kind)
        {
            if (handlingTerminalDisconnect)
            {
                return;
            }

            handlingTerminalDisconnect = true;
            var disconnectedSession = currentSession;
            DetachSession();
            EnsureNetworkStopped();
            SetFailure(kind);
            LoadFrontEndIfNeeded();

            if (disconnectedSession != null && disconnectedSession.IsMember)
            {
                try
                {
                    await disconnectedSession.LeaveAsync();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Session cleanup after disconnect failed: {exception.Message}");
                }
            }

            handlingTerminalDisconnect = false;
        }

        private void HandleConnectionFailure(Exception exception)
        {
            Debug.LogWarning($"Session connection failed: {exception}");
            DetachSession();
            EnsureNetworkStopped();
            SetFailure(SessionFailureMapper.Map(exception));
        }

        private void SubscribeNetworkCallbacks()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            manager.OnClientDisconnectCallback -= OnClientDisconnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void UnsubscribeNetworkCallbacks()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private static void EnsureNetworkStopped()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
            }
        }

        private static void LoadFrontEndIfNeeded()
        {
            if (Application.isPlaying && SceneManager.GetActiveScene().name != "FrontEnd")
            {
                SceneManager.LoadScene("FrontEnd", LoadSceneMode.Single);
            }
        }

        private void SetFailure(SessionFailureKind kind, string message = null)
        {
            LastFailureKind = kind;
            SetState(SessionConnectionState.Failed, message ?? SessionFailureMapper.ToUserMessage(kind));
        }

        private void SetState(SessionConnectionState state, string message)
        {
            State = state;
            StatusMessage = message;
            StatusChanged?.Invoke(state, message);
        }
    }
}
