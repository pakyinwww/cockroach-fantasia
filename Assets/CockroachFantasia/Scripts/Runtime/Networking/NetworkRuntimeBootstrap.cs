using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    [DefaultExecutionOrder(-950)]
    public sealed class NetworkRuntimeBootstrap : MonoBehaviour
    {
        private const string PrefabListResource = "Networking/CockroachNetworkPrefabs";
        private const string DiagnosticAvatarResource = "Networking/DiagnosticAvatar";
        private const string RosterResource = "Networking/NetworkRoster";
        private static NetworkRuntimeBootstrap instance;

        public static NetworkRuntimeBootstrap Instance => instance;
        public NetworkManager Manager { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (instance != null || NetworkManager.Singleton != null)
            {
                return;
            }

            var root = new GameObject(nameof(NetworkRuntimeBootstrap));
            root.AddComponent<NetworkRuntimeBootstrap>();
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

            var transport = gameObject.AddComponent<UnityTransport>();
            Manager = gameObject.AddComponent<NetworkManager>();
            Manager.NetworkConfig = new NetworkConfig();
            Manager.NetworkConfig.NetworkTransport = transport;
            Manager.NetworkConfig.EnableSceneManagement = true;
            Manager.NetworkConfig.TickRate = 30;
            Manager.NetworkConfig.ClientConnectionBufferTimeout = 20;

            var prefabs = Resources.Load<NetworkPrefabsList>(PrefabListResource);
            if (prefabs == null || prefabs.PrefabList.Count == 0)
            {
                Debug.LogError($"Network prefab list is missing at Resources/{PrefabListResource}.");
                return;
            }

            Manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabs);
            Manager.NetworkConfig.PlayerPrefab = Resources.Load<GameObject>(DiagnosticAvatarResource);
            Manager.OnServerStarted += SpawnServerSystems;
        }

        private void OnDestroy()
        {
            if (Manager != null)
            {
                Manager.OnServerStarted -= SpawnServerSystems;
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        private void SpawnServerSystems()
        {
            if (!Manager.IsServer || NetworkRoster.Instance != null)
            {
                return;
            }

            var rosterPrefab = Resources.Load<GameObject>(RosterResource);
            if (rosterPrefab == null)
            {
                Debug.LogError($"Network roster prefab is missing at Resources/{RosterResource}.");
                return;
            }

            var rosterObject = Instantiate(rosterPrefab);
            DontDestroyOnLoad(rosterObject);
            rosterObject.GetComponent<NetworkObject>().Spawn();
        }
    }
}
