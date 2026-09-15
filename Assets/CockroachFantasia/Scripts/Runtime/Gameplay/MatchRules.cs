using UnityEngine;

namespace CockroachFantasia.Gameplay
{
    [CreateAssetMenu(fileName = "MatchRules", menuName = "Cockroach Fantasia/Match Rules")]
    public sealed class MatchRules : ScriptableObject
    {
        [SerializeField, Min(1f)] private float matchDurationSeconds = 240f;
        [SerializeField, Min(1)] private int foodQuotaPoints = 12;
        [SerializeField, Min(0f)] private float countdownSeconds = 3f;
        [SerializeField, Min(0f)] private float respawnDelaySeconds = 3f;

        public float MatchDurationSeconds => matchDurationSeconds;
        public int FoodQuotaPoints => foodQuotaPoints;
        public float CountdownSeconds => countdownSeconds;
        public float RespawnDelaySeconds => respawnDelaySeconds;

        public void Configure(float durationSeconds, int quotaPoints, float countdown, float respawnDelay)
        {
            matchDurationSeconds = Mathf.Max(1f, durationSeconds);
            foodQuotaPoints = Mathf.Max(1, quotaPoints);
            countdownSeconds = Mathf.Max(0f, countdown);
            respawnDelaySeconds = Mathf.Max(0f, respawnDelay);
        }
    }
}
