using System.Collections;
using CockroachFantasia.Characters;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace CockroachFantasia.Tests.PlayMode
{
    public sealed class CockroachMotorTests
    {
        private GameObject instance;
        private GameObject floor;

        [TearDown]
        public void TearDown()
        {
            if (instance != null) Object.DestroyImmediate(instance);
            if (floor != null) Object.DestroyImmediate(floor);
        }

        [Test]
        public void NetworkPrefabKeepsLocalPresentationOffUntilOwnership()
        {
            instance = Object.Instantiate(Resources.Load<GameObject>("Networking/CockroachPlayer"));
            var motor = instance.GetComponent<CockroachMotor>();

            Assert.That(instance.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(instance.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(motor.CarrySocket, Is.Not.Null);
            Assert.That(motor.OwnerCamera.enabled, Is.False);
            Assert.That(motor.OwnerCamera.GetComponent<AudioListener>().enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator MovementAcceleratesCameraRelativeAndRespectsControlGate()
        {
            CreateGroundedMotor(out var motor);
            motor.SetControlState(true, false);
            var start = instance.transform.position;
            for (var index = 0; index < 20; index++)
            {
                motor.SimulateInput(Vector2.up, Vector2.zero, 0.05f);
                yield return null;
            }

            Assert.That(instance.transform.position.z, Is.GreaterThan(start.z + 1.5f));

            var beforeRespawn = instance.transform.position;
            motor.SetControlState(true, true);
            motor.SimulateInput(Vector2.up, Vector2.zero, 1f);
            Assert.That(instance.transform.position, Is.EqualTo(beforeRespawn));

            motor.SetControlState(false, false);
            motor.SimulateInput(Vector2.up, Vector2.zero, 1f);
            Assert.That(instance.transform.position, Is.EqualTo(beforeRespawn));
        }

        [Test]
        public void OrbitClampsPitchAndCameraCollisionPullsIn()
        {
            CreateGroundedMotor(out var motor);
            motor.SetControlState(true, false);
            motor.SetLookSettings(1f, false);
            motor.SimulateInput(Vector2.zero, new Vector2(0f, 1000f), 0.02f);
            Assert.That(motor.CameraPitch, Is.EqualTo(-25f).Within(0.01f));

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "CameraOccluder";
            wall.transform.position = instance.transform.position + new Vector3(0f, 0.17f, -0.4f);
            wall.transform.localScale = new Vector3(1f, 1f, 0.08f);
            Physics.SyncTransforms();
            motor.SimulateInput(Vector2.zero, new Vector2(0f, -43f), 0.02f);
            motor.UpdateCameraPosition();

            var pivot = instance.transform.Find("CameraPivot");
            Assert.That(Vector3.Distance(motor.OwnerCamera.transform.position, pivot.position), Is.LessThan(0.6f));
            Object.DestroyImmediate(wall);
        }

        private void CreateGroundedMotor(out CockroachMotor motor)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetPositionAndRotation(new Vector3(0f, -0.25f, 0f), Quaternion.identity);
            floor.transform.localScale = new Vector3(20f, 0.5f, 20f);
            instance = Object.Instantiate(Resources.Load<GameObject>("Networking/CockroachPlayer"));
            instance.transform.position = new Vector3(0f, 0.01f, 0f);
            Physics.SyncTransforms();
            motor = instance.GetComponent<CockroachMotor>();
        }
    }
}
