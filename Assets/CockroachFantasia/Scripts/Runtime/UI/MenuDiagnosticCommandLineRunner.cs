using System;
using System.Linq;
using System.Threading.Tasks;
using CockroachFantasia.Networking;
using CockroachFantasia.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CockroachFantasia.UI
{
    public sealed class MenuDiagnosticCommandLineRunner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWhenRequested()
        {
            if (!Debug.isDebugBuild) return;
            if (!Environment.GetCommandLineArgs().Contains("-menuSmoke")) return;
            var runner = new GameObject(nameof(MenuDiagnosticCommandLineRunner));
            DontDestroyOnLoad(runner);
            runner.AddComponent<MenuDiagnosticCommandLineRunner>();
        }

        private async void Start()
        {
            try
            {
                await WaitUntilAsync(() => SceneManager.GetActiveScene().name == "FrontEnd",
                    TimeSpan.FromSeconds(30), "FrontEnd after services initialization");
                var canvas = GameObject.Find("FrontEndCanvas");
                var required = new[] { "DisplayName", "RoomCodeInput", "CreateRoom", "JoinRoom", "CopyCode",
                    "LeaveRoom", "Settings", "Quit", "Status", "PlayerCount" };
                if (canvas == null || FindFirstObjectByType<SessionMenuPresenter>() == null ||
                    EventSystem.current == null || required.Any(name => GameObject.Find(name) == null) ||
                    canvas.transform.Find("SettingsPanel/Resolution") == null)
                    throw new InvalidOperationException("The private-room front end is incomplete.");
                if (SessionCoordinator.NormalizeRoomCode("  bcd-678  ") != "BCD678")
                    throw new InvalidOperationException("Room-code normalization is incorrect.");

                var previousSensitivity = PlayerPreferences.MouseSensitivity;
                var previousInvert = PlayerPreferences.InvertY;
                var previousVolume = PlayerPreferences.MasterVolume;
                var previousFullscreen = PlayerPreferences.Fullscreen;
                var previousResolution = PlayerPreferences.Resolution;
                PlayerPreferences.Save(0.31f, true, 0.55f, false, new Vector2Int(1280, 800));
                if (Math.Abs(PlayerPreferences.MouseSensitivity - 0.31f) > 0.001f ||
                    !PlayerPreferences.InvertY || Math.Abs(PlayerPreferences.MasterVolume - 0.55f) > 0.001f ||
                    PlayerPreferences.Resolution != new Vector2Int(1280, 800))
                    throw new InvalidOperationException("Local settings did not persist.");
                PlayerPreferences.Save(previousSensitivity, previousInvert, previousVolume, previousFullscreen,
                    previousResolution);

                SceneManager.LoadScene("Kitchen", LoadSceneMode.Single);
                await WaitUntilAsync(() => FindFirstObjectByType<PauseMenuPresenter>() != null,
                    TimeSpan.FromSeconds(15), "Kitchen pause overlay");
                var pause = FindFirstObjectByType<PauseMenuPresenter>();
                Time.timeScale = 1f;
                pause.Open();
                if (!pause.IsOpen || Math.Abs(Time.timeScale - 1f) > 0.001f)
                    throw new InvalidOperationException("Opening pause changed online simulation time.");
                Debug.Log("MENU_SETTINGS_DIAGNOSTIC_SUCCESS frontend=true code=BCD678 settings=true pauseTimescale=1");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("MENU_SETTINGS_DIAGNOSTIC_FAILED");
                Application.Quit(5);
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string operation)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return;
                await Task.Delay(100);
            }
            throw new TimeoutException("Timed out waiting for " + operation + ".");
        }
    }
}
