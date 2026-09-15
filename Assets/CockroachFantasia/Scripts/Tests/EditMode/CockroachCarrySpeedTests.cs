using CockroachFantasia.Characters;
using NUnit.Framework;
using UnityEngine;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class CockroachCarrySpeedTests
    {
        [Test]
        public void CarryMultiplierChangesMovementAndCanBeRestored()
        {
            var normal = CreateMotor("Normal");
            var burdened = CreateMotor("Burdened");
            try
            {
                burdened.SetCarrySpeedMultiplier(0.70f);

                Assert.That(normal.EffectiveSpeed, Is.EqualTo(3.2f).Within(0.001f));
                Assert.That(burdened.EffectiveSpeed, Is.EqualTo(2.24f).Within(0.001f));
                Assert.That(burdened.CurrentSpeedMultiplier, Is.EqualTo(0.70f).Within(0.001f));
                burdened.SetCarrySpeedMultiplier(1f);
                Assert.That(burdened.CurrentSpeedMultiplier, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(normal.gameObject);
                Object.DestroyImmediate(burdened.gameObject);
            }
        }

        private static CockroachMotor CreateMotor(string name)
        {
            var player = new GameObject(name);
            player.AddComponent<CharacterController>();
            return player.AddComponent<CockroachMotor>();
        }
    }
}
