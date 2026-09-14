using UnityEngine;

namespace CockroachFantasia.Food
{
    [CreateAssetMenu(fileName = "FoodDefinition", menuName = "Cockroach Fantasia/Food Definition")]
    public sealed class FoodDefinition : ScriptableObject
    {
        [SerializeField] private FoodSize size;
        [SerializeField] private string displayName = "Food";
        [SerializeField, Min(1)] private int points = 1;
        [SerializeField, Range(0.1f, 1f)] private float carrySpeedMultiplier = 0.95f;
        [SerializeField] private GameObject networkPrefab;

        public FoodSize Size => size;
        public string DisplayName => displayName;
        public int Points => points;
        public float CarrySpeedMultiplier => carrySpeedMultiplier;
        public GameObject NetworkPrefab => networkPrefab;

        public void Configure(FoodSize foodSize, string label, int pointValue, float speedMultiplier,
            GameObject prefab)
        {
            size = foodSize;
            displayName = label;
            points = Mathf.Max(1, pointValue);
            carrySpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 1f);
            networkPrefab = prefab;
        }
    }
}
