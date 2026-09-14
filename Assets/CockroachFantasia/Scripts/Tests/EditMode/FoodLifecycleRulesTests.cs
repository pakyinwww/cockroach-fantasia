using CockroachFantasia.Food;
using NUnit.Framework;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class FoodLifecycleRulesTests
    {
        [Test]
        public void OneClaimWinsAndOnlyItsCarrierCanDepositOnce()
        {
            var lifecycle = FoodLifecycleState.World;
            var carrier = FoodItem.NoCarrier;

            Assert.That(FoodLifecycleRules.TryClaim(ref lifecycle, ref carrier, 7), Is.True);
            Assert.That(FoodLifecycleRules.TryClaim(ref lifecycle, ref carrier, 8), Is.False);
            Assert.That(carrier, Is.EqualTo(7));
            Assert.That(FoodLifecycleRules.TryDeposit(ref lifecycle, ref carrier, 8), Is.False);
            Assert.That(FoodLifecycleRules.TryDeposit(ref lifecycle, ref carrier, 7), Is.True);
            Assert.That(lifecycle, Is.EqualTo(FoodLifecycleState.Deposited));
            Assert.That(carrier, Is.EqualTo(FoodItem.NoCarrier));
            Assert.That(FoodLifecycleRules.TryDeposit(ref lifecycle, ref carrier, 7), Is.False);
            Assert.That(FoodLifecycleRules.TryDrop(ref lifecycle, ref carrier, 7), Is.False);
            Assert.That(FoodLifecycleRules.TryClaim(ref lifecycle, ref carrier, 8), Is.False);
        }

        [Test]
        public void DropReturnsItemToWorldWithoutDepositingIt()
        {
            var lifecycle = FoodLifecycleState.World;
            var carrier = FoodItem.NoCarrier;
            FoodLifecycleRules.TryClaim(ref lifecycle, ref carrier, 2);

            Assert.That(FoodLifecycleRules.TryDrop(ref lifecycle, ref carrier, 2), Is.True);
            Assert.That(lifecycle, Is.EqualTo(FoodLifecycleState.World));
            Assert.That(carrier, Is.EqualTo(FoodItem.NoCarrier));
        }
    }
}
