using System;
using System.Collections;
using CockroachFantasia.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CockroachFantasia.SinglePlayer
{
    [DefaultExecutionOrder(-850)]
    public sealed class SinglePlayerCoordinator : MonoBehaviour
    {
        private static SinglePlayerCoordinator instance;
        private bool starting;

        public static SinglePlayerCoordinator Instance => instance;
        public bool IsActive { get; private set; }
        public bool IsStarting => starting;
        public SinglePlayerRosterPlan RosterPlan { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;

        public event Action StateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (instance == null && FindFirstObjectByType<SinglePlayerCoordinator>() == null)
                new GameObject(nameof(SinglePlayerCoordinator)).AddComponent<SinglePlayerCoordinator>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void StartGame(PlayerRole playerRole)
        {
            if (starting || IsActive) return;
            StartCoroutine(StartGameRoutine(playerRole));
        }

        public void RequestRematch()
        {
            var manager = NetworkManager.Singleton;
            if (!IsActive || manager == null || !manager.IsHost) return;
            StatusMessage = "Resetting the kitchen…";
            StateChanged?.Invoke();
            manager.SceneManager.LoadScene("Kitchen", LoadSceneMode.Single);
        }

        public void ReturnToMenu()
        {
            StartCoroutine(ReturnToMenuRoutine());
        }

        private IEnumerator StartGameRoutine(PlayerRole playerRole)
        {
            starting = true;
            RosterPlan = new SinglePlayerRosterPlan(playerRole);
            StatusMessage = $"Starting solo as {playerRole}…";
            StateChanged?.Invoke();

            var manager = NetworkManager.Singleton;
            if (manager == null || manager.IsListening)
            {
                Fail("The local game could not start. Please return to the menu and try again.");
                yield break;
            }

            var transport = manager.GetComponent<UnityTransport>();
            if (transport != null) transport.SetConnectionData("127.0.0.1", 7777, "127.0.0.1");
            if (!manager.StartHost())
            {
                Fail("The local game could not start. Another game may already be using the local port.");
                yield break;
            }

            IsActive = true;
            starting = false;
            StatusMessage = "Loading the kitchen…";
            StateChanged?.Invoke();
            yield return null;
            if (manager.SceneManager.LoadScene("Kitchen", LoadSceneMode.Single) != SceneEventProgressStatus.Started)
            {
                manager.Shutdown();
                IsActive = false;
                Fail("The Kitchen scene could not be loaded.");
            }
        }

        private IEnumerator ReturnToMenuRoutine()
        {
            if (starting) yield break;
            starting = true;
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                yield return null;
            }

            IsActive = false;
            RosterPlan = null;
            starting = false;
            StatusMessage = string.Empty;
            SceneManager.LoadScene("FrontEnd", LoadSceneMode.Single);
            StateChanged?.Invoke();
        }

        private void Fail(string message)
        {
            starting = false;
            IsActive = false;
            RosterPlan = null;
            StatusMessage = message;
            StateChanged?.Invoke();
        }
    }
}
