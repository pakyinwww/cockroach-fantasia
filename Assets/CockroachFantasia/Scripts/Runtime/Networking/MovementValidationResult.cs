using UnityEngine;

namespace CockroachFantasia.Networking
{
    public readonly struct MovementValidationResult
    {
        public MovementValidationResult(MovementViolation violation, Vector3 correction)
        {
            Violation = violation;
            Correction = correction;
        }

        public MovementViolation Violation { get; }
        public Vector3 Correction { get; }
        public bool RequiresCorrection => Violation != MovementViolation.None;
    }
}
