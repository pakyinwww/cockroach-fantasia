using CockroachFantasia.Gameplay;
using CockroachFantasia.Networking;
using CockroachFantasia.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using CockroachFantasia.Settings;
using CockroachFantasia.UI;

namespace CockroachFantasia.Characters
{
    [RequireComponent(typeof(NetworkObject), typeof(CharacterController))]
    public sealed class HumanMotor : NetworkBehaviour, IKitchenRecoverable
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float baseSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float acceleration = 24f;
        [SerializeField, Min(0f)] private float gravity = 22f;

        [Header("Look")]
        [SerializeField] private Transform viewPivot;
        [SerializeField] private Camera ownerCamera;
        [SerializeField] private AudioListener ownerListener;
        [SerializeField, Range(0.01f, 1f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private bool invertPitch;
        [SerializeField, Range(-89f, 0f)] private float minimumPitch = -80f;
        [SerializeField, Range(0f, 89f)] private float maximumPitch = 80f;

        [Header("Sockets")]
        [SerializeField] private Transform swatterSocket;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float pitch;
        private bool matchPlaying;
        private bool respawning;
        private NetworkRoleAvatar identity;

        public Camera OwnerCamera => ownerCamera;
        public Transform SwatterSocket => swatterSocket;
        public float Pitch => pitch;
        public bool CanAcceptInput => matchPlaying && !respawning;
        public float MouseSensitivity => mouseSensitivity;
        public bool InvertPitch => invertPitch;

        public void Configure(Transform pivot, Camera localCamera, AudioListener listener, Transform socket)
        {
            viewPivot = pivot;
            ownerCamera = localCamera;
            ownerListener = listener;
            swatterSocket = socket;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            identity = GetComponent<NetworkRoleAvatar>();
            SetLocalPresentation(false);
        }

        public override void OnNetworkSpawn()
        {
            matchPlaying = NetworkGameManager.Instance != null && NetworkGameManager.Instance.AcceptsGameplayRequests;
            RefreshLocalPresentation();
            if (IsOwner && (identity == null || !identity.IsBot))
            {
                DisableFallbackPresentation();
                SetLookSettings(PlayerPreferences.MouseSensitivity, PlayerPreferences.InvertY);
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        public override void OnNetworkDespawn()
        {
            SetLocalPresentation(false);
            if (IsOwner) Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || identity != null && identity.IsBot || !CanAcceptInput ||
                PauseMenuPresenter.IsAnyOpen) return;
            var move = ReadMoveInput();
            var look = Mouse.current?.delta.ReadValue() ?? Vector2.zero;
            SimulateInput(move, look, Time.deltaTime);
        }

        public void SetControlState(bool playing, bool isRespawning)
        {
            matchPlaying = playing;
            respawning = isRespawning;
            if (!CanAcceptInput) planarVelocity = Vector3.zero;
        }

        public void SetLookSettings(float sensitivity, bool invertY)
        {
            mouseSensitivity = Mathf.Clamp(sensitivity, 0.01f, 1f);
            invertPitch = invertY;
        }

        public void SimulateInput(Vector2 moveInput, Vector2 lookInput, float deltaTime)
        {
            if (!CanAcceptInput || deltaTime <= 0f) return;

            transform.Rotate(Vector3.up, lookInput.x * mouseSensitivity, Space.World);
            pitch = Mathf.Clamp(pitch + lookInput.y * mouseSensitivity * (invertPitch ? 1f : -1f),
                minimumPitch, maximumPitch);
            if (viewPivot != null) viewPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            var desiredDirection = Vector3.ClampMagnitude(
                transform.forward * moveInput.y + transform.right * moveInput.x, 1f);
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredDirection * baseSpeed, acceleration * deltaTime);
            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity - gravity * deltaTime;
            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * deltaTime);
        }

        public void SimulateBotMovement(Vector3 worldDirection, float speedMultiplier, float deltaTime)
        {
            if (!CanAcceptInput || deltaTime <= 0f) return;
            worldDirection.y = 0f;
            worldDirection = Vector3.ClampMagnitude(worldDirection, 1f);
            if (worldDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(worldDirection, Vector3.up), 1f - Mathf.Exp(-12f * deltaTime));
            planarVelocity = Vector3.MoveTowards(planarVelocity,
                worldDirection * baseSpeed * Mathf.Clamp01(speedMultiplier), acceleration * deltaTime);
            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity - gravity * deltaTime;
            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * deltaTime);
        }

        public void AimBotAt(Vector3 worldTarget)
        {
            if (viewPivot == null) return;
            var direction = worldTarget - viewPivot.position;
            var horizontal = new Vector2(direction.x, direction.z).magnitude;
            pitch = Mathf.Clamp(Mathf.Atan2(-direction.y, Mathf.Max(0.01f, horizontal)) * Mathf.Rad2Deg,
                minimumPitch, maximumPitch);
            viewPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void RefreshLocalPresentation()
        {
            SetLocalPresentation(IsSpawned && IsOwner && (identity == null || !identity.IsBot));
        }

        public void RecoverTo(Vector3 position)
        {
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = wasEnabled;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        public void SetLocalPresentationForTests(bool enabled)
        {
            SetLocalPresentation(enabled);
        }

        private void SetLocalPresentation(bool enabled)
        {
            if (ownerCamera != null) ownerCamera.enabled = enabled;
            if (ownerListener != null) ownerListener.enabled = enabled;
        }

        private void DisableFallbackPresentation()
        {
            var fallback = GameObject.Find("Main Camera");
            if (fallback == null || fallback.transform.IsChildOf(transform)) return;
            if (fallback.TryGetComponent<Camera>(out var camera)) camera.enabled = false;
            if (fallback.TryGetComponent<AudioListener>(out var listener)) listener.enabled = false;
        }

        private static Vector2 ReadMoveInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;
            return Vector2.ClampMagnitude(new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)), 1f);
        }
    }
}
