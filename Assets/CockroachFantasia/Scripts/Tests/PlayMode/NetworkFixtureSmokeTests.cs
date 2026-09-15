using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CockroachFantasia.Tests.PlayMode
{
    public sealed class NetworkFixtureSmokeTests
    {
        private NetworkTestFixture fixture;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (fixture != null) yield return fixture.Shutdown();
        }

        [UnityTest]
        public IEnumerator HostAndClientConnectAndShutdownWithUnityTransport()
        {
            fixture = new NetworkTestFixture();
            fixture.CreateHostAndClient();

            Assert.That(fixture.Host.StartHost(), Is.True);
            Assert.That(fixture.Client.StartClient(), Is.True);

            var timeout = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => fixture.Client.IsConnectedClient || Time.realtimeSinceStartup >= timeout);

            Assert.That(fixture.Host.IsHost, Is.True);
            Assert.That(fixture.Client.IsConnectedClient, Is.True);
            Assert.That(fixture.Host.ConnectedClients.Count, Is.EqualTo(2));
        }
    }
}
