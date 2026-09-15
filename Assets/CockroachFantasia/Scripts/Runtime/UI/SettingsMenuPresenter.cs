using System.Linq;
using CockroachFantasia.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace CockroachFantasia.UI
{
    public sealed class SettingsMenuPresenter : MonoBehaviour
    {
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text sensitivityValue;
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeValue;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;

        public void Configure(Slider sensitivity, Text sensitivityText, Toggle invertY, Slider volume,
            Text volumeText, Toggle fullscreen, Dropdown resolution, GameObject panel = null,
            Button open = null, Button close = null)
        {
            sensitivitySlider = sensitivity;
            sensitivityValue = sensitivityText;
            invertYToggle = invertY;
            volumeSlider = volume;
            volumeValue = volumeText;
            fullscreenToggle = fullscreen;
            resolutionDropdown = resolution;
            settingsPanel = panel;
            openButton = open;
            closeButton = close;
        }

        private void OnEnable()
        {
            PopulateResolutions();
            RefreshWithoutNotify();
            sensitivitySlider?.onValueChanged.AddListener(OnSettingChanged);
            invertYToggle?.onValueChanged.AddListener(OnSettingChanged);
            volumeSlider?.onValueChanged.AddListener(OnSettingChanged);
            fullscreenToggle?.onValueChanged.AddListener(OnSettingChanged);
            resolutionDropdown?.onValueChanged.AddListener(OnSettingChanged);
            openButton?.onClick.AddListener(Open);
            closeButton?.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            sensitivitySlider?.onValueChanged.RemoveListener(OnSettingChanged);
            invertYToggle?.onValueChanged.RemoveListener(OnSettingChanged);
            volumeSlider?.onValueChanged.RemoveListener(OnSettingChanged);
            fullscreenToggle?.onValueChanged.RemoveListener(OnSettingChanged);
            resolutionDropdown?.onValueChanged.RemoveListener(OnSettingChanged);
            openButton?.onClick.RemoveListener(Open);
            closeButton?.onClick.RemoveListener(Close);
        }

        private void PopulateResolutions()
        {
            if (resolutionDropdown == null) return;
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(PlayerPreferences.SupportedResolutions
                .Select(resolution => $"{resolution.x} × {resolution.y}").ToList());
        }

        private void RefreshWithoutNotify()
        {
            sensitivitySlider?.SetValueWithoutNotify(PlayerPreferences.MouseSensitivity);
            invertYToggle?.SetIsOnWithoutNotify(PlayerPreferences.InvertY);
            volumeSlider?.SetValueWithoutNotify(PlayerPreferences.MasterVolume);
            fullscreenToggle?.SetIsOnWithoutNotify(PlayerPreferences.Fullscreen);
            resolutionDropdown?.SetValueWithoutNotify(PlayerPreferences.ResolutionIndex());
            RefreshLabels();
        }

        private void OnSettingChanged(float value) => Save();
        private void OnSettingChanged(bool value) => Save();
        private void OnSettingChanged(int value) => Save();

        private void Save()
        {
            if (sensitivitySlider == null || invertYToggle == null || volumeSlider == null ||
                fullscreenToggle == null || resolutionDropdown == null) return;
            var resolutionIndex = Mathf.Clamp(resolutionDropdown.value, 0,
                PlayerPreferences.SupportedResolutions.Length - 1);
            PlayerPreferences.Save(sensitivitySlider.value, invertYToggle.isOn, volumeSlider.value,
                fullscreenToggle.isOn, PlayerPreferences.SupportedResolutions[resolutionIndex]);
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (sensitivityValue != null) sensitivityValue.text = $"{PlayerPreferences.MouseSensitivity:0.00}";
            if (volumeValue != null) volumeValue.text = $"{PlayerPreferences.MasterVolume * 100f:0}%";
        }

        private void Open() => settingsPanel?.SetActive(true);
        private void Close() => settingsPanel?.SetActive(false);
    }
}
