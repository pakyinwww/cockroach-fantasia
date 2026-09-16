using System.Linq;
using CockroachFantasia.Characters;
using CockroachFantasia.Food;
using CockroachFantasia.Gameplay;
using CockroachFantasia.World;
using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.SinglePlayer
{
    public sealed class SinglePlayerBotController : MonoBehaviour
    {
        private CockroachMotor cockroach;
        private CockroachFoodCarrier carrier;
        private HumanMotor human;
        private SwatterAttack swatter;
        private float nextDecisionAt;
        private Vector3 targetPosition;
        private CockroachMotor huntTarget;
        private Vector3 lastProgressPosition;
        private float nextProgressCheck;
        private float detourUntil;
        private float detourSign = 1f;

        private void Awake()
        {
            cockroach = GetComponent<CockroachMotor>();
            carrier = GetComponent<CockroachFoodCarrier>();
            human = GetComponent<HumanMotor>();
            swatter = GetComponent<SwatterAttack>();
            lastProgressPosition = transform.position;
        }

        private void Update()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || SinglePlayerCoordinator.Instance == null ||
                !SinglePlayerCoordinator.Instance.IsActive ||
                NetworkGameManager.Instance == null || !NetworkGameManager.Instance.AcceptsGameplayRequests)
                return;

            if (cockroach != null) UpdateCockroach();
            else if (human != null) UpdateHuman();
        }

        private void UpdateCockroach()
        {
            if (cockroach.IsRespawning) return;
            if (Time.time >= nextDecisionAt)
            {
                nextDecisionAt = Time.time + 0.3f;
                targetPosition = carrier.IsCarrying ? FindNestEntrance() : FindNearestFood();
            }

            var offset = targetPosition - transform.position;
            offset.y = 0f;
            cockroach.SimulateBotMovement(AvoidObstacles(offset.normalized), Time.deltaTime);
            if (!carrier.IsCarrying && offset.sqrMagnitude <= 0.7f * 0.7f)
                carrier.TryPickupNearestByServerForBot();
        }

        private void UpdateHuman()
        {
            if (huntTarget == null || huntTarget.IsRespawning || IsInsideProtectedNest(huntTarget.transform.position) ||
                Time.time >= nextDecisionAt)
            {
                huntTarget = FindObjectsByType<CockroachMotor>(FindObjectsSortMode.None)
                    .Where(item => !item.IsRespawning && !IsInsideProtectedNest(item.transform.position))
                    .OrderBy(item => Vector3.SqrMagnitude(item.transform.position - transform.position))
                    .FirstOrDefault();
                nextDecisionAt = Time.time + 2f;
            }
            if (huntTarget == null)
            {
                var entrance = FindNestEntrance();
                var entranceOffset = entrance - transform.position;
                entranceOffset.y = 0f;
                human.SimulateBotMovement(AvoidObstacles(entranceOffset.normalized), 0.55f, Time.deltaTime);
                return;
            }

            var offset = huntTarget.transform.position - transform.position;
            offset.y = 0f;
            human.AimBotAt(huntTarget.transform.position + Vector3.up * 0.12f);
            UpdateStuckRecovery(offset.sqrMagnitude);
            human.SimulateBotMovement(AvoidObstacles(offset.normalized), 1f, Time.deltaTime);
            if (offset.sqrMagnitude <= 1.7f * 1.7f) swatter.TrySwingByServerForBot();
        }

        private void UpdateStuckRecovery(float targetDistanceSquared)
        {
            if (Time.time < nextProgressCheck) return;
            if (targetDistanceSquared > 1.7f * 1.7f &&
                Vector3.Distance(transform.position, lastProgressPosition) < 0.18f)
            {
                detourSign *= -1f;
                detourUntil = Time.time + 1.4f;
            }
            lastProgressPosition = transform.position;
            nextProgressCheck = Time.time + 1f;
        }

        private Vector3 FindNearestFood()
        {
            var food = FindObjectsByType<FoodItem>(FindObjectsSortMode.None)
                .Where(item => item.Lifecycle == FoodLifecycleState.World)
                .OrderBy(item => Vector3.SqrMagnitude(item.transform.position - transform.position))
                .FirstOrDefault();
            return food != null ? food.transform.position : transform.position;
        }

        private static Vector3 FindNestEntrance()
        {
            var nest = FindFirstObjectByType<NestZone>();
            return nest != null && nest.Entrance != null ? nest.Entrance.position : Vector3.zero;
        }

        private static bool IsInsideProtectedNest(Vector3 position)
        {
            var nest = FindFirstObjectByType<NestZone>();
            var zone = nest != null ? nest.GetComponent<BoxCollider>() : null;
            return zone != null && zone.bounds.Contains(position);
        }

        private Vector3 AvoidObstacles(Vector3 desired)
        {
            if (desired.sqrMagnitude < 0.01f) return Vector3.zero;
            if (Time.time < detourUntil)
                return Quaternion.Euler(0f, 85f * detourSign, 0f) * desired;
            var origin = transform.position + Vector3.up * (cockroach != null ? 0.15f : 0.7f);
            if (!Physics.Raycast(origin, desired, cockroach != null ? 0.55f : 0.9f, ~0,
                    QueryTriggerInteraction.Ignore)) return desired;
            var preferred = Quaternion.Euler(0f, 70f * detourSign, 0f) * desired;
            var alternate = Quaternion.Euler(0f, -70f * detourSign, 0f) * desired;
            return !Physics.Raycast(origin, preferred, 0.65f, ~0, QueryTriggerInteraction.Ignore)
                ? preferred
                : alternate;
        }
    }
}
