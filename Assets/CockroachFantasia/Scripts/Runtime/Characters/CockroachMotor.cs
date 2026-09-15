using CockroachFantasia.Gameplay;
using CockroachFantasia.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using CockroachFantasia.Settings;
using CockroachFantasia.UI;

namespace CockroachFantasia.Characters
{
    [RequireComponent(typeof(NetworkObject), typeof(CharacterController))]
    public sealed class CockroachMotor : NetworkBehaviour, IKitchenRecoverable
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float baseSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float acceleration = 18f;
        [SerializeField, Min(0.1f)] private float turnSpeed = 16f;
        [SerializeField, Min(0f)] private float gravity = 18f;

        [Header("Look")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera ownerCamera;
        [SerializeField] private AudioListener ownerListener;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0.16f, -0.78f);
        [SerializeField, Range(0.01f, 1f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private bool invertPitch;
        [SerializeField, Range(-89f, 0f)] private float minimumPitch = -25f;
        [SerializeField, Range(0f, 89f)] private float maximumPitch = 65f;
        [SerializeField, Min(0.01f)] private float cameraCollisionRadius = 0.08f;
        [SerializeField] private LayerMask cameraCollisionMask = ~0;

        [Header("Sockets")]
        [SerializeField] private Transform carrySocket;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float cameraYaw;
        private float cameraPitch = 18f;
        private bool matchPlaying;
        private bool respawning;
        private float carrySpeedMultiplier = 1f;

        public Transform CarrySocket => carrySocket;
        public bool CanAcceptInput => matchPlaying && !respawning;
        public bool IsRespawning => respawning;
        public float CameraPitch => cameraPitch;
        public Camera OwnerCamera => ownerCamera;
        public float CurrentSpeedMultiplier => carrySpeedMultiplier;
        public float EffectiveSpeed => baseSpeed * carrySpeedMultiplier;
        public float MouseSensitivity => mouseSensitivity;
        public bool InvertPitch => invertPitch;

        public void Configure(Transform pivot, Camera localCamera, AudioListener listener, Transform socket)
        {
            cameraPivot = pivot;
            ownerCamera = localCamera;
            ownerListener = listener;
            carrySocket = socket;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            SetLocalPresentation(false);
        }

        public override void OnNetworkSpawn()
        {
            SetLocalPresentation(IsOwner);
            matchPlaying = NetworkGameManager.Instance != null && NetworkGameManager.Instance.AcceptsGameplayRequests;
            cameraYaw = transform.eulerAngles.y;
            if (IsOwner)
            {
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
            if (!IsSpawned || !IsOwner || !CanAcceptInput || PauseMenuPresenter.IsAnyOpen) return;

            var move = ReadMoveInput();
            var look = Mouse.current?.delta.ReadValue() ?? Vector2.zero;
            SimulateInput(move, look, Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || ownerCamera == null || cameraPivot == null) return;
            UpdateCameraPosition();
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

        public void SetCarrySpeedMultiplier(float multiplier)
        {
            carrySpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        }

        public void SimulateInput(Vector2 moveInput, Vector2 lookInput, float deltaTime)
        {
            if (!CanAcceptInput || deltaTime <= 0f) return;

            cameraYaw += lookInput.x * mouseSensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch + lookInput.y * mouseSensitivity * (invertPitch ? 1f : -1f),
                minimumPitch, maximumPitch);

            var cameraForward = cameraPivot != null ? cameraPivot.forward : transform.forward;
            var cameraRight = cameraPivot != null ? cameraPivot.right : transform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();
            var desiredDirection = Vector3.ClampMagnitude(cameraForward * moveInput.y + cameraRight * moveInput.x, 1f);
            var desiredVelocity = desiredDirection * EffectiveSpeed;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, acceleration * deltaTime);

            if (desiredDirection.sqrMagnitude > 0.001f)
            {
                var facing = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, facing, 1f - Mathf.Exp(-turnSpeed * deltaTime));
            }

            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity - gravity * deltaTime;
            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * deltaTime);
            UpdateCameraRotation();
        }

        public void UpdateCameraPosition()
        {
            UpdateCameraRotation();
            var origin = cameraPivot.position;
            var desired = origin + cameraPivot.rotation * cameraOffset;
            var displacement = desired - origin;
            var distance = displacement.magnitude;
            var direction = distance > 0f ? displacement / distance : -cameraPivot.forward;
            var resolvedDistance = distance;
            foreach (var hit in Physics.SphereCastAll(origin, cameraCollisionRadius, direction, distance,
                         cameraCollisionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                resolvedDistance = Mathf.Min(resolvedDistance, Mathf.Max(0.08f, hit.distance - 0.03f));
            }

            ownerCamera.transform.SetPositionAndRotation(origin + direction * resolvedDistance, cameraPivot.rotation);
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

        private void UpdateCameraRotation()
        {
            if (cameraPivot == null) return;
            cameraPivot.position = transform.position + Vector3.up * 0.17f;
            cameraPivot.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        }

        private void SetLocalPresentation(bool enabled)
        {
            if (ownerCamera != null) ownerCamera.enabled = enabled;
            if (ownerListener != null) ownerListener.enabled = enabled;
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
