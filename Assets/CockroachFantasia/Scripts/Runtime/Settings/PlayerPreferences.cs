using CockroachFantasia.Characters;
using UnityEngine;

namespace CockroachFantasia.Settings
{
    public static class PlayerPreferences
    {
        private const string SensitivityKey = "settings.mouseSensitivity";
        private const string InvertYKey = "settings.invertY";
        private const string VolumeKey = "settings.masterVolume";
        private const string FullscreenKey = "settings.fullscreen";
        private const string WidthKey = "settings.resolutionWidth";
        private const string HeightKey = "settings.resolutionHeight";

        public static readonly Vector2Int[] SupportedResolutions =
        {
            new(1280, 800), new(1600, 900), new(1920, 1080)
        };

        public static float MouseSensitivity => PlayerPrefs.GetFloat(SensitivityKey, 0.12f);
        public static bool InvertY => PlayerPrefs.GetInt(InvertYKey, 0) != 0;
        public static float MasterVolume => PlayerPrefs.GetFloat(VolumeKey, 1f);
        public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey, 0) != 0;
        public static Vector2Int Resolution => new(PlayerPrefs.GetInt(WidthKey, 1600),
            PlayerPrefs.GetInt(HeightKey, 900));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyAfterSceneLoad() => ApplyRuntime();

        public static void Save(float sensitivity, bool invertY, float volume, bool fullscreen,
            Vector2Int resolution)
        {
            PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(sensitivity, 0.01f, 1f));
            PlayerPrefs.SetInt(InvertYKey, invertY ? 1 : 0);
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(volume));
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(WidthKey, resolution.x);
            PlayerPrefs.SetInt(HeightKey, resolution.y);
            PlayerPrefs.Save();
            ApplyRuntime();
        }

        public static void ApplyRuntime()
        {
            AudioListener.volume = MasterVolume;
            if (!Application.isEditor && !Application.isBatchMode)
                Screen.SetResolution(Resolution.x, Resolution.y,
                    Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            foreach (var human in Object.FindObjectsByType<HumanMotor>(FindObjectsSortMode.None))
                human.SetLookSettings(MouseSensitivity, InvertY);
            foreach (var cockroach in Object.FindObjectsByType<CockroachMotor>(FindObjectsSortMode.None))
                cockroach.SetLookSettings(MouseSensitivity, InvertY);
        }

        public static int ResolutionIndex()
        {
            var current = Resolution;
            for (var index = 0; index < SupportedResolutions.Length; index++)
                if (SupportedResolutions[index] == current) return index;
            return 1;
        }
    }
}
