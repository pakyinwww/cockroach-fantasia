using System.Collections;
using CockroachFantasia.Characters;
using CockroachFantasia.Settings;
using CockroachFantasia.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CockroachFantasia.Tests.PlayMode
{
    public sealed class MenuAndSettingsTests
    {
        [UnityTest]
        public IEnumerator FrontEndExposesTheCompletePrivateRoomFlowAtSupportedAspectRatios()
        {
            yield return SceneManager.LoadSceneAsync("FrontEnd", LoadSceneMode.Single);
            yield return null;
            Assert.That(Object.FindFirstObjectByType<SessionMenuPresenter>(), Is.Not.Null);
            foreach (var name in new[]
                     {
                         "DisplayName", "RoomCodeInput", "CreateRoom", "JoinRoom", "CopyCode", "LeaveRoom",
                         "Settings", "Quit", "Status", "PlayerCount"
                     })
                Assert.That(GameObject.Find(name), Is.Not.Null, name);
            var settings = GameObject.Find("FrontEndCanvas").transform.Find("SettingsPanel");
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Find("Resolution"), Is.Not.Null);
            Assert.That(settings.Find("Fullscreen"), Is.Not.Null);
            Assert.That(GameObject.Find("FrontEndCanvas").GetComponent<UnityEngine.UI.CanvasScaler>()
                .referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
        }

        [UnityTest]
        public IEnumerator PauseOverlayDoesNotPauseSimulation()
        {
            yield return SceneManager.LoadSceneAsync("Kitchen", LoadSceneMode.Single);
            yield return null;
            var pause = Object.FindFirstObjectByType<PauseMenuPresenter>();
            Time.timeScale = 1f;
            pause.Open();
            Assert.That(pause.IsOpen, Is.True);
            Assert.That(PauseMenuPresenter.IsAnyOpen, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            pause.Close();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void SavedLookSettingsApplyToBothRoleControllers()
        {
            var previousSensitivity = PlayerPreferences.MouseSensitivity;
            var previousInvert = PlayerPreferences.InvertY;
            var previousVolume = PlayerPreferences.MasterVolume;
            var previousFullscreen = PlayerPreferences.Fullscreen;
            var previousResolution = PlayerPreferences.Resolution;
            var humanObject = Object.Instantiate(Resources.Load<GameObject>("Networking/HumanPlayer"));
            var cockroachObject = Object.Instantiate(Resources.Load<GameObject>("Networking/CockroachPlayer"));
            try
            {
                PlayerPreferences.Save(0.37f, true, 0.6f, false, new Vector2Int(1280, 800));
                PlayerPreferences.ApplyRuntime();
                Assert.That(humanObject.GetComponent<HumanMotor>().MouseSensitivity, Is.EqualTo(0.37f).Within(0.001f));
                Assert.That(humanObject.GetComponent<HumanMotor>().InvertPitch, Is.True);
                Assert.That(cockroachObject.GetComponent<CockroachMotor>().MouseSensitivity,
                    Is.EqualTo(0.37f).Within(0.001f));
                Assert.That(cockroachObject.GetComponent<CockroachMotor>().InvertPitch, Is.True);
                Assert.That(AudioListener.volume, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(PlayerPreferences.Resolution, Is.EqualTo(new Vector2Int(1280, 800)));
            }
            finally
            {
                Object.DestroyImmediate(humanObject);
                Object.DestroyImmediate(cockroachObject);
                PlayerPreferences.Save(previousSensitivity, previousInvert, previousVolume, previousFullscreen,
                    previousResolution);
            }
        }
    }
}
