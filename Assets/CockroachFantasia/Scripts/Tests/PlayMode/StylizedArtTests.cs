using System.Linq;
using CockroachFantasia.Characters;
using CockroachFantasia.Food;
using CockroachFantasia.UI;
using NUnit.Framework;
using UnityEngine;

namespace CockroachFantasia.Tests.PlayMode
{
    public sealed class StylizedArtTests
    {
        [Test]
        public void RoleAndFoodPrefabsHaveDistinctColliderFreeArtSilhouettes()
        {
            var cockroach = Resources.Load<GameObject>("Networking/CockroachPlayer");
            var human = Resources.Load<GameObject>("Networking/HumanPlayer");
            Assert.That(cockroach.GetComponent<StylizedCharacterAnimator>(), Is.Not.Null);
            Assert.That(cockroach.transform.Find("ArtRootV2/Head"), Is.Not.Null);
            Assert.That(cockroach.transform.Find("ArtRootV2/AntennaLeft"), Is.Not.Null);
            Assert.That(cockroach.transform.Find("ArtRootV2/LegRight3"), Is.Not.Null);
            Assert.That(human.GetComponent<StylizedCharacterAnimator>(), Is.Not.Null);
            Assert.That(human.transform.Find("ArtRootV2/Apron"), Is.Not.Null);
            Assert.That(human.transform.Find("ViewPivot/SwatterSocket/SwatterVisual/FoamPad"), Is.Not.Null);

            foreach (var size in new[] { FoodSize.Small, FoodSize.Medium, FoodSize.Large })
            {
                var food = Resources.Load<GameObject>($"Food/{size}Food");
                Assert.That(food.transform.Find("FoodArtDetailsV2"), Is.Not.Null, size.ToString());
                Assert.That(food.transform.Find("FoodArtDetailsV2").GetComponentsInChildren<Collider>(), Is.Empty);
            }
        }

        [Test]
        public void ComicBurstIsBloodlessAndHasReadableCaption()
        {
            var burst = ComicVfx.SpawnBurst(Vector3.zero, Color.yellow, "WHOMP!");
            try
            {
                Assert.That(burst.GetComponentsInChildren<Collider>().Where(collider => collider.enabled), Is.Empty);
                Assert.That(burst.GetComponentsInChildren<TextMesh>().Single().text, Is.EqualTo("WHOMP!"));
                Assert.That(burst.GetComponentsInChildren<Renderer>(), Has.Length.GreaterThanOrEqualTo(8));
            }
            finally
            {
                Object.DestroyImmediate(burst);
            }
        }
    }
}
