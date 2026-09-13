using System.Linq;
using CockroachFantasia.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

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

        private NetworkRoster roster;

        public void Configure(Button[] buttons, Text[] labels, InputField nameInput, Text status)
        {
            seatButtons = buttons;
            seatLabels = labels;
            displayNameInput = nameInput;
            statusLabel = status;
        }

        private void OnEnable()
        {
            for (var index = 0; index < seatButtons?.Length && index < SeatOrder.Length; index++)
            {
                var captured = SeatOrder[index];
                seatButtons[index].onClick.AddListener(() => roster?.RequestSeat(captured));
            }

            displayNameInput?.onEndEdit.AddListener(SetDisplayName);
            TryBindRoster();
        }

        private void OnDisable()
        {
            if (roster != null)
            {
                roster.Changed -= Refresh;
                roster.LocalSeatRequestResolved -= OnSeatRequestResolved;
            }

            for (var index = 0; index < seatButtons?.Length; index++)
            {
                seatButtons[index]?.onClick.RemoveAllListeners();
            }

            displayNameInput?.onEndEdit.RemoveListener(SetDisplayName);
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
            Refresh();
        }

        private void SetDisplayName(string value)
        {
            roster?.SetLocalDisplayName(value);
        }

        private void OnSeatRequestResolved(bool accepted, string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
                statusLabel.color = accepted ? new Color(0.75f, 1f, 0.7f) : new Color(1f, 0.72f, 0.55f);
            }
        }

        private void Refresh()
        {
            if (roster == null)
            {
                return;
            }

            var localId = NetworkManager.Singleton?.LocalClientId ?? ulong.MaxValue;
            var snapshot = roster.Entries;
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
