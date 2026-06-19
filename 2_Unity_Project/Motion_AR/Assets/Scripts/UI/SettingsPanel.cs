using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MotionAR.Core;

namespace MotionAR.UI
{
    /// <summary>
    /// Settings panel UI that allows users to configure application settings.
    /// <para>
    /// Binds to <see cref="SettingsManager"/> for persistence and provides
    /// sliders for volume, toggles for features, and buttons for mode switching.
    /// </para>
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Volume Controls")]
        [SerializeField]
        [Tooltip("Slider for SFX volume.")]
        private Slider _sfxVolumeSlider;

        [SerializeField]
        [Tooltip("Slider for UI sounds volume.")]
        private Slider _uiVolumeSlider;

        [SerializeField]
        [Tooltip("Label displaying current SFX volume percentage.")]
        private TextMeshProUGUI _sfxVolumeLabel;

        [SerializeField]
        [Tooltip("Label displaying current UI volume percentage.")]
        private TextMeshProUGUI _uiVolumeLabel;

        [Header("Input Mode")]
        [SerializeField]
        [Tooltip("Toggle for Mock mode (mouse input).")]
        private Toggle _mockModeToggle;

        [SerializeField]
        [Tooltip("Toggle for MediaPipe mode (webcam hand tracking).")]
        private Toggle _mediaPipeModeToggle;

        [Header("Toggles")]
        [SerializeField]
        [Tooltip("Toggle for webcam mirror.")]
        private Toggle _mirrorWebcamToggle;

        [SerializeField]
        [Tooltip("Toggle for showing hint text.")]
        private Toggle _showHintsToggle;

        [Header("Hand Size")]
        [SerializeField]
        [Tooltip("Slider for hand cursor size.")]
        private Slider _handSizeSlider;

        [SerializeField]
        [Tooltip("Label displaying current hand size multiplier.")]
        private TextMeshProUGUI _handSizeLabel;

        [Header("Buttons")]
        [SerializeField]
        [Tooltip("Button to reset all settings to defaults.")]
        private Button _resetButton;

        [SerializeField]
        [Tooltip("Button to go back to the previous screen.")]
        private Button _backButton;

        private void OnEnable()
        {
            LoadCurrentSettings();
            BindListeners();
        }

        private void OnDisable()
        {
            UnbindListeners();
        }

        /// <summary>
        /// Loads current values from SettingsManager and updates UI elements.
        /// </summary>
        private void LoadCurrentSettings()
        {
            var settings = SettingsManager.Instance;
            if (settings == null) return;

            // Volume sliders.
            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.value = settings.SFXVolume;
                UpdateSFXLabel(settings.SFXVolume);
            }
            if (_uiVolumeSlider != null)
            {
                _uiVolumeSlider.value = settings.UIVolume;
                UpdateUILabel(settings.UIVolume);
            }

            // Input mode toggles.
            if (_mockModeToggle != null)
                _mockModeToggle.isOn = settings.CurrentInputMode == SettingsManager.InputMode.Mock;
            if (_mediaPipeModeToggle != null)
                _mediaPipeModeToggle.isOn = settings.CurrentInputMode == SettingsManager.InputMode.MediaPipe;

            // Feature toggles.
            if (_mirrorWebcamToggle != null)
                _mirrorWebcamToggle.isOn = settings.MirrorWebcam;
            if (_showHintsToggle != null)
                _showHintsToggle.isOn = settings.ShowHints;

            // Hand size.
            if (_handSizeSlider != null)
            {
                _handSizeSlider.value = settings.HandSize;
                UpdateHandSizeLabel(settings.HandSize);
            }
        }

        private void BindListeners()
        {
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            if (_uiVolumeSlider != null) _uiVolumeSlider.onValueChanged.AddListener(OnUIVolumeChanged);
            if (_mockModeToggle != null) _mockModeToggle.onValueChanged.AddListener(OnMockModeToggled);
            if (_mirrorWebcamToggle != null) _mirrorWebcamToggle.onValueChanged.AddListener(OnMirrorToggled);
            if (_showHintsToggle != null) _showHintsToggle.onValueChanged.AddListener(OnShowHintsToggled);
            if (_handSizeSlider != null) _handSizeSlider.onValueChanged.AddListener(OnHandSizeChanged);
            if (_resetButton != null) _resetButton.onClick.AddListener(OnResetClicked);
            if (_backButton != null) _backButton.onClick.AddListener(OnBackClicked);
        }

        private void UnbindListeners()
        {
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
            if (_uiVolumeSlider != null) _uiVolumeSlider.onValueChanged.RemoveListener(OnUIVolumeChanged);
            if (_mockModeToggle != null) _mockModeToggle.onValueChanged.RemoveListener(OnMockModeToggled);
            if (_mirrorWebcamToggle != null) _mirrorWebcamToggle.onValueChanged.RemoveListener(OnMirrorToggled);
            if (_showHintsToggle != null) _showHintsToggle.onValueChanged.RemoveListener(OnShowHintsToggled);
            if (_handSizeSlider != null) _handSizeSlider.onValueChanged.RemoveListener(OnHandSizeChanged);
            if (_resetButton != null) _resetButton.onClick.RemoveListener(OnResetClicked);
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);
        }

        // ────────────────────── Callbacks ──────────────────────

        private void OnSFXVolumeChanged(float value)
        {
            SettingsManager.Instance?.SetSFXVolume(value);
            UpdateSFXLabel(value);
        }

        private void OnUIVolumeChanged(float value)
        {
            SettingsManager.Instance?.SetUIVolume(value);
            UpdateUILabel(value);
        }

        private void OnMockModeToggled(bool isOn)
        {
            if (isOn)
            {
                SettingsManager.Instance?.SetInputMode(SettingsManager.InputMode.Mock);
                if (_mediaPipeModeToggle != null) _mediaPipeModeToggle.isOn = false;
            }
        }

        private void OnMirrorToggled(bool isOn)
        {
            SettingsManager.Instance?.SetMirrorWebcam(isOn);
        }

        private void OnShowHintsToggled(bool isOn)
        {
            SettingsManager.Instance?.SetShowHints(isOn);
        }

        private void OnHandSizeChanged(float value)
        {
            SettingsManager.Instance?.SetHandSize(value);
            UpdateHandSizeLabel(value);
        }

        private void OnResetClicked()
        {
            SettingsManager.Instance?.ResetToDefaults();
            LoadCurrentSettings();
        }

        private void OnBackClicked()
        {
            AppFlowManager.Instance?.GoBack();
        }

        // ────────────────────── Label Helpers ──────────────────────

        private void UpdateSFXLabel(float value)
        {
            if (_sfxVolumeLabel != null) _sfxVolumeLabel.text = $"{value * 100:F0}%";
        }

        private void UpdateUILabel(float value)
        {
            if (_uiVolumeLabel != null) _uiVolumeLabel.text = $"{value * 100:F0}%";
        }

        private void UpdateHandSizeLabel(float value)
        {
            if (_handSizeLabel != null) _handSizeLabel.text = $"×{value:F1}";
        }
    }
}
