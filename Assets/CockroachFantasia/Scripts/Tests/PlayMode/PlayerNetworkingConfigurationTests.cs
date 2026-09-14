using CockroachFantasia.Networking;
using NUnit.Framework;
using UnityEngine;

namespace CockroachFantasia.Tests.PlayMode
{
    public sealed class PlayerNetworkingConfigurationTests
    {
        [TestCase("Networking/HumanPlayer")]
        [TestCase("Networking/CockroachPlayer")]
        public void PlayerPrefabsUseLeanOwnerAuthoritativeTransform(string resource)
        {
            var prefab = Resources.Load<GameObject>(resource);
            var transformSync = prefab.GetComponent<OwnerNetworkTransform>();

            Assert.That(transformSync, Is.Not.Null);
            Assert.That(transformSync.IsServerAuthoritative(), Is.False);
            Assert.That(transformSync.SyncPositionX && transformSync.SyncPositionY && transformSync.SyncPositionZ, Is.True);
            Assert.That(transformSync.SyncRotAngleX, Is.False);
            Assert.That(transformSync.SyncRotAngleY, Is.True);
            Assert.That(transformSync.SyncRotAngleZ, Is.False);
            Assert.That(transformSync.SyncScaleX || transformSync.SyncScaleY || transformSync.SyncScaleZ, Is.False);
            Assert.That(transformSync.Interpolate, Is.True);
            Assert.That(transformSync.UseUnreliableDeltas, Is.True);
            Assert.That(transformSync.UseHalfFloatPrecision, Is.True);
            Assert.That(prefab.GetComponent<MovementSanityMonitor>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkRoleAvatar>(), Is.Not.Null);
        }
    }
}
