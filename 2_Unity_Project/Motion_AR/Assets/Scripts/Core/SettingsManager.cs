using UnityEngine;
using System;

namespace MotionAR.Core
{
    /// <summary>
    /// Manages persistent user settings: volume, input mode, language, and
    /// accessibility options. Saves to PlayerPrefs automatically.
    /// <para>
    /// Access via <see cref="Instance"/> singleton. Changes are saved immediately
    /// and events are fired for UI to react.
    /// </para>
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        // PlayerPrefs keys.
        private const string KEY_SFX_VOLUME = "settings_sfx_volume";
        private const string KEY_UI_VOLUME = "settings_ui_volume";
        private const string KEY_INPUT_MODE = "settings_input_mode";
        private const string KEY_MIRROR_WEBCAM = "settings_mirror_webcam";
        private const string KEY_SHOW_HINTS = "settings_show_hints";
        private const string KEY_HAND_SIZE = "settings_hand_size";

        /// <summary>Input mode: Mock (mouse) or MediaPipe (live hand tracking).</summary>
        public enum InputMode { Mock = 0, MediaPipe = 1 }

        // ─────────────── Singleton ───────────────

        public static SettingsManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }

        // ─────────────── Settings Properties ───────────────

        /// <summary>SFX volume [0, 1].</summary>
        public float SFXVolume { get; private set; } = 0.7f;

        /// <summary>UI sounds volume [0, 1].</summary>
        public float UIVolume { get; private set; } = 0.5f;

        /// <summary>Current input mode.</summary>
        public InputMode CurrentInputMode { get; private set; } = InputMode.Mock;

        /// <summary>Whether to mirror the webcam feed.</summary>
        public bool MirrorWebcam { get; private set; } = true;

        /// <summary>Whether to show hint text in steps.</summary>
        public bool ShowHints { get; private set; } = true;

        /// <summary>Hand cursor size multiplier.</summary>
        public float HandSize { get; private set; } = 1f;

        // ─────────────── Events ───────────────

        /// <summary>Fired when any setting changes.</summary>
        public event Action OnSettingsChanged;

        /// <summary>Fired when input mode changes. Provides the new mode.</summary>
        public event Action<InputMode> OnInputModeChanged;

        // ─────────────── Setters ───────────────

        public void SetSFXVolume(float volume)
        {
            SFXVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(KEY_SFX_VOLUME, SFXVolume);
            OnSettingsChanged?.Invoke();
        }

        public void SetUIVolume(float volume)
        {
            UIVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(KEY_UI_VOLUME, UIVolume);
            OnSettingsChanged?.Invoke();
        }

        public void SetInputMode(InputMode mode)
        {
            CurrentInputMode = mode;
            PlayerPrefs.SetInt(KEY_INPUT_MODE, (int)mode);
            OnInputModeChanged?.Invoke(mode);
            OnSettingsChanged?.Invoke();
        }

        public void SetMirrorWebcam(bool mirror)
        {
            MirrorWebcam = mirror;
            PlayerPrefs.SetInt(KEY_MIRROR_WEBCAM, mirror ? 1 : 0);
            OnSettingsChanged?.Invoke();
        }

        public void SetShowHints(bool show)
        {
            ShowHints = show;
            PlayerPrefs.SetInt(KEY_SHOW_HINTS, show ? 1 : 0);
            OnSettingsChanged?.Invoke();
        }

        public void SetHandSize(float size)
        {
            HandSize = Mathf.Clamp(size, 0.5f, 2f);
            PlayerPrefs.SetFloat(KEY_HAND_SIZE, HandSize);
            OnSettingsChanged?.Invoke();
        }

        // ─────────────── Load/Save ───────────────

        private void LoadSettings()
        {
            SFXVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, 0.7f);
            UIVolume = PlayerPrefs.GetFloat(KEY_UI_VOLUME, 0.5f);
            CurrentInputMode = (InputMode)PlayerPrefs.GetInt(KEY_INPUT_MODE, 0);
            MirrorWebcam = PlayerPrefs.GetInt(KEY_MIRROR_WEBCAM, 1) == 1;
            ShowHints = PlayerPrefs.GetInt(KEY_SHOW_HINTS, 1) == 1;
            HandSize = PlayerPrefs.GetFloat(KEY_HAND_SIZE, 1f);

            Debug.Log($"[SettingsManager] Loaded: SFX={SFXVolume:F1}, UI={UIVolume:F1}, " +
                      $"Input={CurrentInputMode}, Mirror={MirrorWebcam}");
        }

        /// <summary>
        /// Resets all settings to defaults and saves.
        /// </summary>
        public void ResetToDefaults()
        {
            SFXVolume = 0.7f;
            UIVolume = 0.5f;
            CurrentInputMode = InputMode.Mock;
            MirrorWebcam = true;
            ShowHints = true;
            HandSize = 1f;

            PlayerPrefs.SetFloat(KEY_SFX_VOLUME, SFXVolume);
            PlayerPrefs.SetFloat(KEY_UI_VOLUME, UIVolume);
            PlayerPrefs.SetInt(KEY_INPUT_MODE, (int)CurrentInputMode);
            PlayerPrefs.SetInt(KEY_MIRROR_WEBCAM, 1);
            PlayerPrefs.SetInt(KEY_SHOW_HINTS, 1);
            PlayerPrefs.SetFloat(KEY_HAND_SIZE, HandSize);
            PlayerPrefs.Save();

            OnSettingsChanged?.Invoke();
            Debug.Log("[SettingsManager] Reset to defaults.");
        }
    }
}
