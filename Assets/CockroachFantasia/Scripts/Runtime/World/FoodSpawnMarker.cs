using UnityEngine;

namespace CockroachFantasia.World
{
    public sealed class FoodSpawnMarker : MonoBehaviour
    {
        [SerializeField] private int index;
        [SerializeField] private FoodRiskLevel risk;

        public int Index => index;
        public FoodRiskLevel Risk => risk;

        public void Configure(int markerIndex, FoodRiskLevel riskLevel)
        {
            index = markerIndex;
            risk = riskLevel;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = risk switch
            {
                FoodRiskLevel.Low => new Color(0.35f, 1f, 0.45f, 0.9f),
                FoodRiskLevel.Medium => new Color(1f, 0.75f, 0.2f, 0.9f),
                _ => new Color(1f, 0.25f, 0.2f, 0.9f)
            };
            Gizmos.DrawSphere(transform.position, 0.18f);
            Gizmos.DrawWireSphere(transform.position, 0.32f);
        }
    }
}
