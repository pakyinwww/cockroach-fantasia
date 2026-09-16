using CockroachFantasia.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CockroachFantasia.SinglePlayer;

namespace CockroachFantasia.UI
{
    public sealed class PauseMenuPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject overlay;
        [SerializeField] private GameObject leaveConfirmation;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button confirmLeaveButton;
        [SerializeField] private Button cancelLeaveButton;

        public static bool IsAnyOpen { get; private set; }
        public bool IsOpen => overlay != null && overlay.activeSelf;

        public void Configure(GameObject pauseOverlay, GameObject confirmation, Button resume, Button leave,
            Button confirmLeave, Button cancelLeave)
        {
            overlay = pauseOverlay;
            leaveConfirmation = confirmation;
            resumeButton = resume;
            leaveButton = leave;
            confirmLeaveButton = confirmLeave;
            cancelLeaveButton = cancelLeave;
        }

        private void OnEnable()
        {
            resumeButton?.onClick.AddListener(Close);
            leaveButton?.onClick.AddListener(ShowLeaveConfirmation);
            confirmLeaveButton?.onClick.AddListener(ConfirmLeave);
            cancelLeaveButton?.onClick.AddListener(HideLeaveConfirmation);
        }

        private void OnDisable()
        {
            resumeButton?.onClick.RemoveListener(Close);
            leaveButton?.onClick.RemoveListener(ShowLeaveConfirmation);
            confirmLeaveButton?.onClick.RemoveListener(ConfirmLeave);
            cancelLeaveButton?.onClick.RemoveListener(HideLeaveConfirmation);
            IsAnyOpen = false;
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (leaveConfirmation != null && leaveConfirmation.activeSelf) HideLeaveConfirmation();
            else if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            overlay?.SetActive(true);
            leaveConfirmation?.SetActive(false);
            IsAnyOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // Deliberately do not touch Time.timeScale: the online match continues.
        }

        public void Close()
        {
            overlay?.SetActive(false);
            IsAnyOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ShowLeaveConfirmation() => leaveConfirmation?.SetActive(true);
        private void HideLeaveConfirmation() => leaveConfirmation?.SetActive(false);

        private async void ConfirmLeave()
        {
            if (SinglePlayerCoordinator.Instance != null && SinglePlayerCoordinator.Instance.IsActive)
                SinglePlayerCoordinator.Instance.ReturnToMenu();
            else if (SessionCoordinator.Instance != null) await SessionCoordinator.Instance.LeaveAsync();
        }
    }
}
