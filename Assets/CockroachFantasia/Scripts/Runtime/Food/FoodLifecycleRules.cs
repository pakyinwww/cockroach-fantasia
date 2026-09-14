namespace CockroachFantasia.Food
{
    public static class FoodLifecycleRules
    {
        public static bool TryClaim(ref FoodLifecycleState lifecycle, ref ulong carrierClientId, ulong claimant)
        {
            if (lifecycle != FoodLifecycleState.World || carrierClientId != FoodItem.NoCarrier) return false;
            carrierClientId = claimant;
            lifecycle = FoodLifecycleState.Carried;
            return true;
        }

        public static bool TryDrop(ref FoodLifecycleState lifecycle, ref ulong carrierClientId, ulong claimant)
        {
            if (lifecycle != FoodLifecycleState.Carried || carrierClientId != claimant) return false;
            carrierClientId = FoodItem.NoCarrier;
            lifecycle = FoodLifecycleState.World;
            return true;
        }

        public static bool TryDeposit(ref FoodLifecycleState lifecycle, ref ulong carrierClientId, ulong claimant)
        {
            if (lifecycle != FoodLifecycleState.Carried || carrierClientId != claimant) return false;
            carrierClientId = FoodItem.NoCarrier;
            lifecycle = FoodLifecycleState.Deposited;
            return true;
        }
    }
}
