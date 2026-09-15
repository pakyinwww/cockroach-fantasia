using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CockroachFantasia.Characters;
using CockroachFantasia.Food;
using CockroachFantasia.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using CockroachFantasia.UI;
using CockroachFantasia.Audio;

namespace CockroachFantasia.Gameplay
{
    [RequireComponent(typeof(NetworkObject), typeof(HumanMotor), typeof(NetworkRoleAvatar))]
    public sealed class SwatterAttack : NetworkBehaviour
    {
        public const float WindupSeconds = 0.25f;
        public const float ReachMetres = 1.8f;
        public const float SweepRadiusMetres = 0.55f;

        private HumanMotor motor;
        private NetworkRoleAvatar identity;
        private double lastAcceptedServerTime = double.NegativeInfinity;
        private float localSwingStartedAt = float.NegativeInfinity;
        private float localCooldownEndsAt = float.NegativeInfinity;
        private Quaternion socketRestRotation;

        public uint ConfirmedImpactSequence { get; private set; }
        public int LastConfirmedHitCount { get; private set; }
        public float LocalCooldownRemaining => Mathf.Max(0f, localCooldownEndsAt - Time.unscaledTime);
        public bool IsLocallyReady => LocalCooldownRemaining <= 0f;
        public event Action<IReadOnlyList<CockroachMotor>> ServerHitConfirmed;

        private void Awake()
        {
            motor = GetComponent<HumanMotor>();
            identity = GetComponent<NetworkRoleAvatar>();
        }

        public override void OnNetworkSpawn()
        {
            if (motor.SwatterSocket != null) socketRestRotation = motor.SwatterSocket.localRotation;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            if (motor.CanAcceptInput && !PauseMenuPresenter.IsAnyOpen &&
                Mouse.current?.leftButton.wasPressedThisFrame == true)
                RequestSwing();
            UpdateLocalSwingPresentation();
        }

        public void RequestSwing()
        {
            if (!IsSpawned || !IsOwner || !motor.CanAcceptInput || !IsLocallyReady) return;
            BeginLocalSwingPresentation();
            localCooldownEndsAt = Time.unscaledTime + (float)SwatterAttackRules.CooldownSeconds;
            RequestSwingRpc();
        }

        public void RequestSwingForDiagnostics()
        {
            if (!IsSpawned || !IsOwner || !Debug.isDebugBuild) return;
            BeginLocalSwingPresentation();
            localCooldownEndsAt = Time.unscaledTime + (float)SwatterAttackRules.CooldownSeconds;
            RequestSwingRpc();
        }

        [Rpc(SendTo.Server)]
        private void RequestSwingRpc(RpcParams rpcParams = default)
        {
            var game = NetworkGameManager.Instance;
            if (game == null || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            var serverTime = NetworkManager.ServerTime.Time;
            if (!SwatterAttackRules.CanStart(identity.Seat, game.Phase, serverTime, lastAcceptedServerTime)) return;
            lastAcceptedServerTime = serverTime;
            PlaySwingRpc(transform.position);
            StartCoroutine(ResolveAfterWindup());
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlaySwingRpc(Vector3 position) => GameAudio.Play(GameAudioCue.SwatterSwing, position);

        private IEnumerator ResolveAfterWindup()
        {
            yield return new WaitForSeconds(WindupSeconds);
            var game = NetworkGameManager.Instance;
            if (!IsServer || game == null || !game.AcceptsGameplayRequests || identity.Seat != LobbySeat.Human)
                yield break;

            var origin = motor.SwatterSocket != null
                ? motor.SwatterSocket.position
                : transform.position + Vector3.up * 1.3f;
            var direction = motor.SwatterSocket != null ? motor.SwatterSocket.forward : transform.forward;
            direction.Normalize();
            if (Debug.isDebugBuild && Environment.GetCommandLineArgs().Contains("-swatterSmoke"))
            {
                var diagnosticTargets = UnityEngine.Object.FindObjectsByType<CockroachMotor>(
                    FindObjectsSortMode.None).OrderBy(target => target.OwnerClientId).ToArray();
                if (Environment.GetCommandLineArgs().Contains("-respawnSmoke"))
                {
                    var cargo = UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None)
                        .Where(food => food.Lifecycle == FoodLifecycleState.World)
                        .OrderBy(food => food.NetworkObjectId).Skip(1).First();
                    if (!diagnosticTargets[0].GetComponent<CockroachFoodCarrier>()
                            .PreparePickupForRespawnDiagnosticsByServer(cargo))
                        throw new InvalidOperationException("Could not stage carried cargo for respawn diagnostic.");
                }
                for (var index = 0; index < diagnosticTargets.Length; index++)
                    diagnosticTargets[index].RecoverTo(origin + direction * (0.55f + 0.35f * index));
                Physics.SyncTransforms();
            }

            var end = origin + direction * ReachMetres;
            var targets = Physics.OverlapCapsule(origin, end, SweepRadiusMetres, ~0,
                    QueryTriggerInteraction.Ignore)
                .Select(hit => hit.GetComponentInParent<CockroachMotor>())
                .Where(target => target != null && target.IsSpawned)
                .GroupBy(target => target.NetworkObjectId)
                .Select(group => group.First())
                .ToArray();
            if (targets.Length == 0) yield break;

            foreach (var target in targets)
                target.GetComponent<CockroachRespawn>()?.ApplyConfirmedHitByServer();
            ServerHitConfirmed?.Invoke(targets);
            var impact = targets.Aggregate(Vector3.zero, (sum, target) => sum + target.transform.position) /
                         targets.Length;
            ConfirmImpactRpc(impact, targets.Length, ConfirmedImpactSequence + 1);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ConfirmImpactRpc(Vector3 position, int hitCount, uint sequence)
        {
            ConfirmedImpactSequence = sequence;
            LastConfirmedHitCount = hitCount;
            var flash = ComicVfx.SpawnBurst(position, new Color(1f, 0.35f, 0.2f), "WHOMP!");
            flash.name = "SwatterImpact";
            GameAudio.Play(GameAudioCue.HarmlessImpact, position);
        }

        private void BeginLocalSwingPresentation()
        {
            localSwingStartedAt = Time.unscaledTime;
        }

        private void UpdateLocalSwingPresentation()
        {
            var socket = motor.SwatterSocket;
            if (socket == null) return;
            var elapsed = Time.unscaledTime - localSwingStartedAt;
            if (elapsed < 0f || elapsed > WindupSeconds * 2f)
            {
                socket.localRotation = socketRestRotation;
                return;
            }

            var normalized = elapsed / (WindupSeconds * 2f);
            var angle = normalized < 0.5f
                ? Mathf.Lerp(0f, -55f, normalized * 2f)
                : Mathf.Lerp(-55f, 0f, (normalized - 0.5f) * 2f);
            socket.localRotation = socketRestRotation * Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
