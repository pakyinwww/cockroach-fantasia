using System.Collections;
using System.Linq;
using CockroachFantasia.Food;
using CockroachFantasia.Gameplay;
using CockroachFantasia.Networking;
using CockroachFantasia.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CockroachFantasia.Tests.PlayMode
{
    public sealed class KitchenLayoutSmokeTests
    {
        [UnityTest]
        public IEnumerator KitchenContainsBoundedThreeRouteLayoutAndEditableMarkers()
        {
            yield return SceneManager.LoadSceneAsync("Kitchen", LoadSceneMode.Single);
            yield return null;

            var foods = Object.FindObjectsByType<FoodSpawnMarker>(FindObjectsSortMode.None);
            Assert.That(foods, Has.Length.EqualTo(18));
            Assert.That(foods.Select(marker => marker.Index).Distinct().Count(), Is.EqualTo(18));
            Assert.That(foods.Count(marker => marker.Risk == FoodRiskLevel.Low), Is.EqualTo(6));
            Assert.That(foods.Count(marker => marker.Risk == FoodRiskLevel.Medium), Is.EqualTo(6));
            Assert.That(foods.Count(marker => marker.Risk == FoodRiskLevel.High), Is.EqualTo(6));

            var spawns = Object.FindObjectsByType<KitchenSpawnMarker>(FindObjectsSortMode.None);
            Assert.That(spawns, Has.Length.EqualTo(4));
            Assert.That(spawns.Select(marker => marker.Seat), Is.EquivalentTo(new[]
            {
                LobbySeat.Human, LobbySeat.CockroachOne, LobbySeat.CockroachTwo, LobbySeat.CockroachThree
            }));

            Assert.That(GameObject.Find("Route_A_Floor_Long"), Is.Not.Null);
            Assert.That(GameObject.Find("Route_B_Counter_High"), Is.Not.Null);
            Assert.That(GameObject.Find("Route_C_Cabinet_Tunnel"), Is.Not.Null);
            Assert.That(GameObject.Find("Sight_Blocker_A").GetComponent<Collider>(), Is.Not.Null);
            Assert.That(GameObject.Find("Sight_Blocker_B").GetComponent<Collider>(), Is.Not.Null);

            foreach (var bound in new[] { "Bound_Back", "Bound_Front", "Bound_Left", "Bound_Right" })
                Assert.That(GameObject.Find(bound).GetComponent<Collider>(), Is.Not.Null, bound);

            Assert.That(Object.FindFirstObjectByType<NestZone>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<KitchenRecoveryVolume>(), Is.Not.Null);
            var gameManager = Object.FindFirstObjectByType<NetworkGameManager>();
            Assert.That(gameManager, Is.Not.Null);
            Assert.That(gameManager.GetComponent<Unity.Netcode.NetworkObject>(), Is.Not.Null);
            Assert.That(gameManager.Rules, Is.Not.Null);
            Assert.That(gameManager.Rules.MatchDurationSeconds, Is.EqualTo(240f));
            Assert.That(gameManager.Rules.FoodQuotaPoints, Is.EqualTo(12));
            Assert.That(gameManager.Rules.CountdownSeconds, Is.EqualTo(3f));
            Assert.That(gameManager.Rules.RespawnDelaySeconds, Is.EqualTo(3f));
            var foodSpawner = Object.FindFirstObjectByType<KitchenFoodSpawner>();
            Assert.That(foodSpawner, Is.Not.Null);
            Assert.That(FoodConfigurationValidator.TryValidate(foodSpawner.SpawnSet, out var foodPoints,
                out var foodRejection), Is.True, foodRejection);
            Assert.That(foodPoints, Is.EqualTo(18));
            Assert.That(GameObject.Find("Huge_Mug").GetComponent<Collider>(), Is.Null);
            Assert.That(GameObject.Find("Fruit_Bowl").GetComponent<Collider>(), Is.Null);

            var nestCeiling = GameObject.Find("Nest_Ceiling").transform;
            var clearance = nestCeiling.position.y - nestCeiling.localScale.y * 0.5f;
            Assert.That(clearance, Is.GreaterThan(0.25f), "Cockroach clearance");
            Assert.That(clearance, Is.LessThan(1f), "Human capsule exclusion");
        }
    }
}
