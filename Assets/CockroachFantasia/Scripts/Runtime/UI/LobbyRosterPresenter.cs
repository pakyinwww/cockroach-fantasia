using System.Linq;
using CockroachFantasia.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CockroachFantasia.UI
{
    public sealed class LobbyRosterPresenter : MonoBehaviour
    {
        private static readonly LobbySeat[] SeatOrder =
        {
            LobbySeat.Human,
            LobbySeat.CockroachOne,
            LobbySeat.CockroachTwo,
            LobbySeat.CockroachThree
        };

        [SerializeField] private Button[] seatButtons;
        [SerializeField] private Text[] seatLabels;
        [SerializeField] private InputField displayNameInput;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button readyButton;
        [SerializeField] private Text readyLabel;
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject loadingPanel;

        private NetworkRoster roster;

        public void Configure(Button[] buttons, Text[] labels, InputField nameInput, Text status,
            Button ready, Text readyText, Button start, GameObject loading)
        {
            seatButtons = buttons;
            seatLabels = labels;
            displayNameInput = nameInput;
            statusLabel = status;
            readyButton = ready;
            readyLabel = readyText;
            startButton = start;
            loadingPanel = loading;
        }

        private void OnEnable()
        {
            for (var index = 0; index < seatButtons?.Length && index < SeatOrder.Length; index++)
            {
                var captured = SeatOrder[index];
                seatButtons[index].onClick.AddListener(() => roster?.RequestSeat(captured));
            }

            displayNameInput?.onEndEdit.AddListener(SetDisplayName);
            readyButton?.onClick.AddListener(ToggleReady);
            startButton?.onClick.AddListener(StartMatch);
            if (displayNameInput != null) displayNameInput.text = SessionIdentity.DisplayName;
            EventSystem.current?.SetSelectedGameObject(displayNameInput?.gameObject ?? readyButton?.gameObject);
            TryBindRoster();
        }

        private void OnDisable()
        {
            if (roster != null)
            {
                roster.Changed -= Refresh;
                roster.LocalSeatRequestResolved -= OnSeatRequestResolved;
                roster.LocalLobbyActionResolved -= OnLobbyActionResolved;
            }

            for (var index = 0; index < seatButtons?.Length; index++)
            {
                seatButtons[index]?.onClick.RemoveAllListeners();
            }

            displayNameInput?.onEndEdit.RemoveListener(SetDisplayName);
            readyButton?.onClick.RemoveListener(ToggleReady);
            startButton?.onClick.RemoveListener(StartMatch);
        }

        private void Update()
        {
            if (roster == null)
            {
                TryBindRoster();
            }
        }

        private void TryBindRoster()
        {
            if (NetworkRoster.Instance == null || roster == NetworkRoster.Instance)
            {
                return;
            }

            roster = NetworkRoster.Instance;
            roster.Changed += Refresh;
            roster.LocalSeatRequestResolved += OnSeatRequestResolved;
            roster.LocalLobbyActionResolved += OnLobbyActionResolved;
            Refresh();
        }

        private void SetDisplayName(string value)
        {
            SessionIdentity.DisplayName = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
            roster?.SetLocalDisplayName(value);
        }

        private void OnSeatRequestResolved(bool accepted, string message)
        {
            ShowStatus(accepted, message);
        }

        private void OnLobbyActionResolved(bool accepted, string message)
        {
            ShowStatus(accepted, message);
        }

        private void ShowStatus(bool accepted, string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
                statusLabel.color = accepted ? new Color(0.75f, 1f, 0.7f) : new Color(1f, 0.72f, 0.55f);
            }
        }

        private void ToggleReady()
        {
            if (roster == null || NetworkManager.Singleton == null) return;
            roster.SetLocalReady(!roster.TryGetEntry(NetworkManager.Singleton.LocalClientId, out var entry) || !entry.Ready);
        }

        private void StartMatch()
        {
            roster?.RequestStartMatch();
        }

        private void Refresh()
        {
            if (roster == null)
            {
                return;
            }

            var localId = NetworkManager.Singleton?.LocalClientId ?? ulong.MaxValue;
            var snapshot = roster.Entries;
            var localEntry = default(RosterEntry);
            var hasLocalEntry = localId != ulong.MaxValue && roster.TryGetEntry(localId, out localEntry);
            for (var index = 0; index < SeatOrder.Length && index < seatLabels?.Length; index++)
            {
                var seat = SeatOrder[index];
                var occupant = snapshot.FirstOrDefault(entry => entry.Connected && entry.Seat == seat);
                var occupied = occupant.Connected;
                seatLabels[index].text = $"{GetSeatLabel(seat)}\n{(occupied ? occupant.DisplayName.ToString() : "Open")}";
                if (seatButtons != null && index < seatButtons.Length)
                {
                    seatButtons[index].interactable = !roster.IsLocked && (!occupied || occupant.ClientId == localId);
                }
            }


            if (readyButton != null)
                readyButton.interactable = !roster.IsLocked && hasLocalEntry && RosterRules.IsSelectableSeat(localEntry.Seat);
            if (readyLabel != null)
                readyLabel.text = hasLocalEntry && localEntry.Ready ? "NOT READY" : "READY UP";
            if (startButton != null)
                startButton.interactable = roster.CanLocalHostStart;
            if (loadingPanel != null)
                loadingPanel.SetActive(roster.IsLoading);
        }

        private static string GetSeatLabel(LobbySeat seat)
        {
            return seat switch
            {
                LobbySeat.Human => "HUMAN",
                LobbySeat.CockroachOne => "COCKROACH 1",
                LobbySeat.CockroachTwo => "COCKROACH 2",
                LobbySeat.CockroachThree => "COCKROACH 3",
                _ => "ROLE"
            };
        }
    }
}
