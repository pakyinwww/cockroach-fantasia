using System.Linq;
using CockroachFantasia.Food;
using NUnit.Framework;
using UnityEngine;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class FoodConfigurationValidatorTests
    {
        [Test]
        public void FixedSetContainsThreeSizesAndExactlyEighteenPoints()
        {
            var fixture = CreateFixture();
            try
            {
                Assert.That(FoodConfigurationValidator.TryValidate(fixture.Set, out var points, out var rejection),
                    Is.True, rejection);
                Assert.That(points, Is.EqualTo(18));
                Assert.That(fixture.Set.Entries.GroupBy(entry => entry.Definition.Size)
                    .ToDictionary(group => group.Key, group => group.Count()), Is.EquivalentTo(
                    new System.Collections.Generic.Dictionary<FoodSize, int>
                    {
                        [FoodSize.Small] = 3, [FoodSize.Medium] = 3, [FoodSize.Large] = 3
                    }));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void MalformedTotalsAndDuplicateMarkersAreRejected()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Large.Configure(FoodSize.Large, "Large", 1, 0.7f, fixture.LargePrefab);
                Assert.That(FoodConfigurationValidator.TryValidate(fixture.Set, out var points, out var rejection),
                    Is.False);
                Assert.That(points, Is.EqualTo(12));
                Assert.That(rejection, Does.Contain("expected 18"));

                fixture.Large.Configure(FoodSize.Large, "Large", 3, 0.7f, fixture.LargePrefab);
                var entries = fixture.Set.Entries.ToArray();
                entries[1].MarkerIndex = entries[0].MarkerIndex;
                fixture.Set.Configure(entries);
                Assert.That(FoodConfigurationValidator.TryValidate(fixture.Set, out _, out rejection), Is.False);
                Assert.That(rejection, Does.Contain("duplicated"));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static FoodFixture CreateFixture()
        {
            var fixture = new FoodFixture();
            fixture.SmallPrefab = CreatePrefabLikeObject("Small");
            fixture.MediumPrefab = CreatePrefabLikeObject("Medium");
            fixture.LargePrefab = CreatePrefabLikeObject("Large");
            fixture.Small = CreateDefinition(FoodSize.Small, 1, 0.95f, fixture.SmallPrefab);
            fixture.Medium = CreateDefinition(FoodSize.Medium, 2, 0.85f, fixture.MediumPrefab);
            fixture.Large = CreateDefinition(FoodSize.Large, 3, 0.70f, fixture.LargePrefab);
            fixture.Set = ScriptableObject.CreateInstance<FoodSpawnSet>();
            fixture.Set.Configure(new[]
            {
                new FoodSpawnEntry(0, fixture.Small), new FoodSpawnEntry(1, fixture.Small),
                new FoodSpawnEntry(2, fixture.Small), new FoodSpawnEntry(6, fixture.Medium),
                new FoodSpawnEntry(7, fixture.Medium), new FoodSpawnEntry(8, fixture.Medium),
                new FoodSpawnEntry(12, fixture.Large), new FoodSpawnEntry(13, fixture.Large),
                new FoodSpawnEntry(14, fixture.Large)
            });
            return fixture;
        }

        private static GameObject CreatePrefabLikeObject(string name)
        {
            var instance = new GameObject(name);
            instance.AddComponent<FoodItem>();
            return instance;
        }

        private static FoodDefinition CreateDefinition(FoodSize size, int points, float speed, GameObject prefab)
        {
            var definition = ScriptableObject.CreateInstance<FoodDefinition>();
            definition.Configure(size, size.ToString(), points, speed, prefab);
            return definition;
        }

        private sealed class FoodFixture
        {
            public FoodSpawnSet Set;
            public FoodDefinition Small;
            public FoodDefinition Medium;
            public FoodDefinition Large;
            public GameObject SmallPrefab;
            public GameObject MediumPrefab;
            public GameObject LargePrefab;

            public void Dispose()
            {
                Object.DestroyImmediate(Set);
                Object.DestroyImmediate(Small);
                Object.DestroyImmediate(Medium);
                Object.DestroyImmediate(Large);
                Object.DestroyImmediate(SmallPrefab);
                Object.DestroyImmediate(MediumPrefab);
                Object.DestroyImmediate(LargePrefab);
            }
        }
    }
}
