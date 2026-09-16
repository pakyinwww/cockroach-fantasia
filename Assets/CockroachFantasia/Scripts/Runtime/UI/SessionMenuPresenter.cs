using CockroachFantasia.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CockroachFantasia.SinglePlayer;

namespace CockroachFantasia.UI
{
    public sealed class SessionMenuPresenter : MonoBehaviour
    {
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private InputField displayNameInput;
        [SerializeField] private Text roomCodeLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Text playerCountLabel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button copyButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private Button soloHumanButton;
        [SerializeField] private Button soloCockroachButton;
        [SerializeField] private Button quitButton;

        private SessionCoordinator coordinator;
        private bool appliedDisplayName;

        public void Configure(InputField roomCode, InputField displayName, Text roomCodeText, Text status,
            Text players, Button host, Button join, Button copy, Button leave, Button lobby, Button soloHuman,
            Button soloCockroach, Button quit)
        {
            roomCodeInput = roomCode;
            displayNameInput = displayName;
            roomCodeLabel = roomCodeText;
            statusLabel = status;
            playerCountLabel = players;
            hostButton = host;
            joinButton = join;
            copyButton = copy;
            leaveButton = leave;
            lobbyButton = lobby;
            soloHumanButton = soloHuman;
            soloCockroachButton = soloCockroach;
            quitButton = quit;
        }

        private void OnEnable()
        {
            coordinator = SessionCoordinator.Instance;
            if (coordinator == null)
            {
                return;
            }

            coordinator.StatusChanged += OnStatusChanged;
            coordinator.SessionChanged += Refresh;
            if (SinglePlayerCoordinator.Instance != null)
                SinglePlayerCoordinator.Instance.StateChanged += Refresh;
            hostButton?.onClick.AddListener(Host);
            joinButton?.onClick.AddListener(Join);
            copyButton?.onClick.AddListener(coordinator.CopyRoomCode);
            leaveButton?.onClick.AddListener(Leave);
            lobbyButton?.onClick.AddListener(OpenLobby);
            soloHumanButton?.onClick.AddListener(StartSoloHuman);
            soloCockroachButton?.onClick.AddListener(StartSoloCockroach);
            quitButton?.onClick.AddListener(Quit);
            roomCodeInput?.onEndEdit.AddListener(NormalizeRoomCode);
            displayNameInput?.onEndEdit.AddListener(SetDisplayName);
            if (displayNameInput != null) displayNameInput.text = SessionIdentity.DisplayName;
            EventSystem.current?.SetSelectedGameObject(hostButton?.gameObject);
            Refresh();
        }

        private void OnDisable()
        {
            if (coordinator == null)
            {
                return;
            }

            coordinator.StatusChanged -= OnStatusChanged;
            coordinator.SessionChanged -= Refresh;
            if (SinglePlayerCoordinator.Instance != null)
                SinglePlayerCoordinator.Instance.StateChanged -= Refresh;
            hostButton?.onClick.RemoveListener(Host);
            joinButton?.onClick.RemoveListener(Join);
            copyButton?.onClick.RemoveListener(coordinator.CopyRoomCode);
            leaveButton?.onClick.RemoveListener(Leave);
            lobbyButton?.onClick.RemoveListener(OpenLobby);
            quitButton?.onClick.RemoveListener(Quit);
            soloHumanButton?.onClick.RemoveListener(StartSoloHuman);
            soloCockroachButton?.onClick.RemoveListener(StartSoloCockroach);
            roomCodeInput?.onEndEdit.RemoveListener(NormalizeRoomCode);
            displayNameInput?.onEndEdit.RemoveListener(SetDisplayName);
        }

        private async void Host()
        {
            await coordinator.HostPrivateSessionAsync();
            Refresh();
        }

        private async void Join()
        {
            await coordinator.JoinPrivateSessionAsync(roomCodeInput?.text);
            Refresh();
        }

        private async void Leave()
        {
            await coordinator.LeaveAsync();
            Refresh();
        }

        private void OpenLobby()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
        }

        private static void Quit() => Application.Quit();

        private void NormalizeRoomCode(string value)
        {
            if (roomCodeInput != null) roomCodeInput.SetTextWithoutNotify(SessionCoordinator.NormalizeRoomCode(value));
        }

        private void SetDisplayName(string value)
        {
            SessionIdentity.DisplayName = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
            appliedDisplayName = false;
            ApplyDisplayNameToRoster();
        }

        private void OnStatusChanged(SessionConnectionState state, string message)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (coordinator == null)
            {
                return;
            }

            if (roomCodeLabel != null)
            {
                roomCodeLabel.text = string.IsNullOrEmpty(coordinator.RoomCode)
                    ? "Room code: —"
                    : $"Room code: {coordinator.RoomCode}";
            }

            if (statusLabel != null)
            {
                var solo = SinglePlayerCoordinator.Instance;
                statusLabel.text = solo != null && !string.IsNullOrEmpty(solo.StatusMessage)
                    ? solo.StatusMessage
                    : coordinator.StatusMessage;
            }

            if (playerCountLabel != null)
            {
                playerCountLabel.text = $"Players: {coordinator.PlayerCount}/{SessionCoordinator.MaximumPlayers}";
            }

            var busy = coordinator.State == SessionConnectionState.Connecting ||
                       coordinator.State == SessionConnectionState.Leaving;
            var connected = coordinator.State == SessionConnectionState.Connected;
            if (hostButton != null) hostButton.interactable = !busy && !connected;
            if (joinButton != null) joinButton.interactable = !busy && !connected;
            var soloBusy = SinglePlayerCoordinator.Instance != null &&
                           SinglePlayerCoordinator.Instance.IsStarting;
            if (soloHumanButton != null) soloHumanButton.interactable = !busy && !connected && !soloBusy;
            if (soloCockroachButton != null) soloCockroachButton.interactable = !busy && !connected && !soloBusy;
            if (copyButton != null) copyButton.interactable = !string.IsNullOrEmpty(coordinator.RoomCode);
            if (leaveButton != null) leaveButton.interactable = connected;
            if (lobbyButton != null)
            {
                lobbyButton.gameObject.SetActive(connected);
                lobbyButton.interactable = connected && NetworkManager.Singleton != null &&
                                           NetworkManager.Singleton.IsHost;
                var label = lobbyButton.GetComponentInChildren<Text>();
                if (label != null) label.text = lobbyButton.interactable ? "OPEN ROLE LOBBY" : "WAITING FOR HOST";
            }
            ApplyDisplayNameToRoster();
        }

        private void StartSoloHuman() => StartSolo(PlayerRole.Human);
        private void StartSoloCockroach() => StartSolo(PlayerRole.Cockroach);

        private void StartSolo(PlayerRole role)
        {
            SinglePlayerCoordinator.Instance?.StartGame(role);
            Refresh();
        }

        private void Update()
        {
            if (!appliedDisplayName) ApplyDisplayNameToRoster();
        }

        private void ApplyDisplayNameToRoster()
        {
            if (NetworkRoster.Instance == null || NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsListening) return;
            NetworkRoster.Instance.SetLocalDisplayName(SessionIdentity.DisplayName);
            appliedDisplayName = true;
        }
    }
}
