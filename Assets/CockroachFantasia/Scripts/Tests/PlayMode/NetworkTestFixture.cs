using System.Net;
using System.Net.Sockets;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace CockroachFantasia.Tests.PlayMode
{
    internal sealed class NetworkTestFixture
    {
        public NetworkManager Host { get; private set; }
        public NetworkManager Client { get; private set; }

        public void CreateHostAndClient()
        {
            foreach (var networkObject in Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
                Object.DestroyImmediate(networkObject.gameObject);
            if (NetworkManager.Singleton != null)
                Object.DestroyImmediate(NetworkManager.Singleton.gameObject);

            var port = ReservePort();
            Host = CreateManager("Test Host", port);
            Client = CreateManager("Test Client", port);
        }

        public IEnumerator Shutdown()
        {
            if (Client != null && Client.IsListening) Client.Shutdown();
            yield return null;
            if (Host != null && Host.IsListening) Host.Shutdown();
            yield return null;
            Destroy(Client);
            Destroy(Host);
            Client = null;
            Host = null;
            yield return null;
        }

        private static NetworkManager CreateManager(string name, ushort port)
        {
            var root = new GameObject(name);
            var transport = root.AddComponent<UnityTransport>();
            var manager = root.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                TickRate = 30
            };
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            return manager;
        }

        private static ushort ReservePort()
        {
            using var socket = new UdpClient(AddressFamily.InterNetwork);
            socket.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            var port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            return port;
        }

        private static void Destroy(NetworkManager manager)
        {
            if (manager == null) return;
            Object.Destroy(manager.gameObject);
        }
    }
}
