using CockroachFantasia.Networking;
using NUnit.Framework;
using UnityEngine;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class MovementSanityValidatorTests
    {
        private static readonly Bounds Bounds = new Bounds(Vector3.zero, new Vector3(10f, 4f, 10f));

        [Test]
        public void OrdinaryAndBriefSpikeMotionIsNotCorrected()
        {
            var validator = new MovementSanityValidator(Bounds, 4f);
            validator.Reset(Vector3.zero);

            Assert.That(validator.Evaluate(new Vector3(0f, 0f, 0.35f), 0.1f).RequiresCorrection, Is.False);
            Assert.That(validator.Evaluate(new Vector3(0f, 0f, 1.15f), 0.1f).RequiresCorrection, Is.False);
            Assert.That(validator.Evaluate(new Vector3(0f, 0f, 1.35f), 0.1f).RequiresCorrection, Is.False);
        }

        [Test]
        public void SustainedImpossibleSpeedReturnsLastValidPosition()
        {
            var validator = new MovementSanityValidator(Bounds, 4f);
            validator.Reset(Vector3.zero);
            Assert.That(validator.Evaluate(new Vector3(0f, 0f, 0.8f), 0.1f).RequiresCorrection, Is.False);
            Assert.That(validator.Evaluate(new Vector3(0f, 0f, 1.6f), 0.1f).RequiresCorrection, Is.False);
            var result = validator.Evaluate(new Vector3(0f, 0f, 2.4f), 0.1f);

            Assert.That(result.Violation, Is.EqualTo(MovementViolation.SustainedSpeed));
            Assert.That(result.Correction, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TeleportAndOutOfBoundsAreCorrectedImmediately()
        {
            var validator = new MovementSanityValidator(Bounds, 4f);
            validator.Reset(Vector3.zero);

            Assert.That(validator.Evaluate(new Vector3(0f, 0f, 4.8f), 0.1f).Violation,
                Is.EqualTo(MovementViolation.Teleport));
            Assert.That(validator.Evaluate(new Vector3(8f, 0f, 0f), 0.1f).Violation,
                Is.EqualTo(MovementViolation.OutOfBounds));
        }
    }
}
