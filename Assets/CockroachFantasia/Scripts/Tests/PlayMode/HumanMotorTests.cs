using System.Collections;
using CockroachFantasia.Characters;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CockroachFantasia.Tests.PlayMode
{
    public sealed class HumanMotorTests
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
        public void NetworkPrefabKeepsFirstPersonPresentationOwnerOnly()
        {
            instance = Object.Instantiate(Resources.Load<GameObject>("Networking/HumanPlayer"));
            var motor = instance.GetComponent<HumanMotor>();
            var controller = instance.GetComponent<CharacterController>();

            Assert.That(instance.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(motor.SwatterSocket, Is.Not.Null);
            Assert.That(motor.OwnerCamera.enabled, Is.False);
            Assert.That(motor.OwnerCamera.GetComponent<AudioListener>().enabled, Is.False);
            Assert.That(motor.OwnerCamera.nearClipPlane, Is.LessThan(controller.radius));
        }

        [UnityTest]
        public IEnumerator MovementAndLookRespectPitchAndGameplayGate()
        {
            CreateGroundedMotor(out var motor);
            motor.SetControlState(true, false);
            motor.SetLookSettings(1f, false);
            motor.SimulateInput(Vector2.zero, new Vector2(0f, -1000f), 0.02f);
            Assert.That(motor.Pitch, Is.EqualTo(80f).Within(0.01f));

            var start = instance.transform.position;
            for (var index = 0; index < 20; index++)
            {
                motor.SimulateInput(Vector2.up, Vector2.zero, 0.05f);
                yield return null;
            }
            Assert.That(instance.transform.position.z, Is.GreaterThan(start.z + 2f));

            var gatedPosition = instance.transform.position;
            motor.SetControlState(false, false);
            motor.SimulateInput(Vector2.up, Vector2.zero, 1f);
            Assert.That(instance.transform.position, Is.EqualTo(gatedPosition));
        }

        [UnityTest]
        public IEnumerator HumanCannotEnterAndCanSlideOffNestEntrance()
        {
            yield return SceneManager.LoadSceneAsync("Kitchen", LoadSceneMode.Single);
            instance = Object.Instantiate(Resources.Load<GameObject>("Networking/HumanPlayer"));
            instance.transform.SetPositionAndRotation(new Vector3(-6.15f, 0.02f, 4f), Quaternion.identity);
            var motor = instance.GetComponent<HumanMotor>();
            motor.SetControlState(true, false);
            Physics.SyncTransforms();

            for (var index = 0; index < 50; index++)
            {
                motor.SimulateInput(Vector2.up, Vector2.zero, 0.04f);
                yield return null;
            }
            Assert.That(instance.transform.position.z, Is.LessThan(4.35f), "Human crossed the nest exclusion.");

            var blockedX = instance.transform.position.x;
            for (var index = 0; index < 35; index++)
            {
                motor.SimulateInput(Vector2.right, Vector2.zero, 0.04f);
                yield return null;
            }
            Assert.That(instance.transform.position.x, Is.GreaterThan(blockedX + 1f),
                "Controller became stuck instead of sliding away from the nest boundary.");
        }

        private void CreateGroundedMotor(out HumanMotor motor)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(20f, 0.5f, 20f);
            instance = Object.Instantiate(Resources.Load<GameObject>("Networking/HumanPlayer"));
            instance.transform.position = new Vector3(0f, 0.02f, 0f);
            Physics.SyncTransforms();
            motor = instance.GetComponent<HumanMotor>();
        }
    }
}
