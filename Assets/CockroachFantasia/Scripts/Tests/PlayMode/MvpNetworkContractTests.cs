using CockroachFantasia.Characters;
using CockroachFantasia.Food;
using CockroachFantasia.Gameplay;
using CockroachFantasia.Networking;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Tests.PlayMode
{
    public sealed class MvpNetworkContractTests
    {
        [Test]
        public void RolePrefabsExposeOwnerOnlyCameraAndAuthoritativeGameplayComponents()
        {
            var human = Resources.Load<GameObject>("Networking/HumanPlayer");
            var roach = Resources.Load<GameObject>("Networking/CockroachPlayer");
            Assert.That(human.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(human.GetComponent<HumanMotor>().OwnerCamera.enabled, Is.False);
            Assert.That(human.GetComponent<SwatterAttack>(), Is.Not.Null);
            Assert.That(roach.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(roach.GetComponent<CockroachMotor>().OwnerCamera.enabled, Is.False);
            Assert.That(roach.GetComponent<CockroachFoodCarrier>(), Is.Not.Null);
            Assert.That(roach.GetComponent<CockroachRespawn>(), Is.Not.Null);
        }

        [Test]
        public void NetworkedFoodPrefabsCannotDuplicateLifecycleOwnership()
        {
            foreach (var resource in new[] { "Food/SmallFood", "Food/MediumFood", "Food/LargeFood" })
            {
                var prefab = Resources.Load<GameObject>(resource);
                Assert.That(prefab.GetComponents<NetworkObject>(), Has.Length.EqualTo(1));
                Assert.That(prefab.GetComponents<FoodItem>(), Has.Length.EqualTo(1));
                Assert.That(prefab.GetComponent<FoodItem>().Definition, Is.Not.Null);
            }
        }
    }
}
