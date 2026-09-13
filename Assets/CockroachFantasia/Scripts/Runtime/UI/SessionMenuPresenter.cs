using CockroachFantasia.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace CockroachFantasia.UI
{
    public sealed class SessionMenuPresenter : MonoBehaviour
    {
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private Text roomCodeLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Text playerCountLabel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button copyButton;
        [SerializeField] private Button leaveButton;

        private SessionCoordinator coordinator;

        private void OnEnable()
        {
            coordinator = SessionCoordinator.Instance;
            if (coordinator == null)
            {
                return;
            }

            coordinator.StatusChanged += OnStatusChanged;
            coordinator.SessionChanged += Refresh;
            hostButton?.onClick.AddListener(Host);
            joinButton?.onClick.AddListener(Join);
            copyButton?.onClick.AddListener(coordinator.CopyRoomCode);
            leaveButton?.onClick.AddListener(Leave);
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
            hostButton?.onClick.RemoveListener(Host);
            joinButton?.onClick.RemoveListener(Join);
            copyButton?.onClick.RemoveListener(coordinator.CopyRoomCode);
            leaveButton?.onClick.RemoveListener(Leave);
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
                statusLabel.text = coordinator.StatusMessage;
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
            if (copyButton != null) copyButton.interactable = !string.IsNullOrEmpty(coordinator.RoomCode);
            if (leaveButton != null) leaveButton.interactable = connected;
        }
    }
}
