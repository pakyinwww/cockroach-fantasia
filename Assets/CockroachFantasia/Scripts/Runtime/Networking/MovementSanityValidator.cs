using UnityEngine;

namespace CockroachFantasia.Networking
{
    public sealed class MovementSanityValidator
    {
        private readonly Bounds playableBounds;
        private readonly float maximumSpeed;
        private readonly float teleportDistance;
        private readonly float sustainedSeconds;
        private bool initialized;
        private Vector3 lastObserved;
        private Vector3 lastValid;
        private float overSpeedTime;

        public MovementSanityValidator(Bounds bounds, float roleSpeed, float speedAllowance = 1.75f,
            float clearTeleportDistance = 4.5f, float sustainedViolationSeconds = 0.3f)
        {
            playableBounds = bounds;
            maximumSpeed = roleSpeed * speedAllowance;
            teleportDistance = clearTeleportDistance;
            sustainedSeconds = sustainedViolationSeconds;
        }

        public MovementValidationResult Evaluate(Vector3 position, float deltaTime)
        {
            if (!initialized)
            {
                Reset(playableBounds.Contains(position) ? position : playableBounds.ClosestPoint(position));
                return playableBounds.Contains(position)
                    ? default
                    : new MovementValidationResult(MovementViolation.OutOfBounds, lastValid);
            }

            if (!playableBounds.Contains(position))
                return Reject(MovementViolation.OutOfBounds);

            var distance = Vector3.Distance(lastObserved, position);
            if (distance > teleportDistance)
                return Reject(MovementViolation.Teleport);

            var speed = deltaTime > 0f ? distance / deltaTime : 0f;
            lastObserved = position;
            if (speed > maximumSpeed)
            {
                overSpeedTime += Mathf.Max(0f, deltaTime);
                if (overSpeedTime >= sustainedSeconds)
                    return Reject(MovementViolation.SustainedSpeed);
                return default;
            }

            overSpeedTime = 0f;
            lastValid = position;
            return default;
        }

        public void Reset(Vector3 position)
        {
            initialized = true;
            lastObserved = position;
            lastValid = position;
            overSpeedTime = 0f;
        }

        private MovementValidationResult Reject(MovementViolation violation)
        {
            var correction = lastValid;
            Reset(correction);
            return new MovementValidationResult(violation, correction);
        }
    }
}
