using UnityEngine;
using MotionAR.Core;

namespace MotionAR.UI
{
    /// <summary>
    /// Centralized audio feedback manager that plays sound effects for
    /// various training events: snap success, snap error, pinch, release,
    /// step advance, lesson complete, and UI interactions.
    /// <para>
    /// Attach to a persistent GameObject with an AudioSource.
    /// All sounds are optional — missing clips are silently skipped.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioFeedbackManager : MonoBehaviour
    {
        [Header("Interaction Sounds")]
        [SerializeField] private AudioClip _pinchSound;
        [SerializeField] private AudioClip _releaseSound;
        [SerializeField] private AudioClip _snapSuccessSound;
        [SerializeField] private AudioClip _snapErrorSound;

        [Header("Session Sounds")]
        [SerializeField] private AudioClip _stepAdvanceSound;
        [SerializeField] private AudioClip _lessonCompleteSound;
        [SerializeField] private AudioClip _mistakeSound;

        [Header("UI Sounds")]
        [SerializeField] private AudioClip _buttonHoverSound;
        [SerializeField] private AudioClip _buttonActivateSound;
        [SerializeField] private AudioClip _panelSlideSound;

        [Header("Settings")]
        [SerializeField] [Range(0f, 1f)] private float _sfxVolume = 0.7f;
        [SerializeField] [Range(0f, 1f)] private float _uiVolume = 0.5f;

        private AudioSource _audioSource;

        /// <summary>Global singleton instance.</summary>
        public static AudioFeedbackManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        private void OnEnable()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnStepChanged += _ => PlayStepAdvance();
                SessionManager.Instance.OnMistakeRecorded += PlayMistake;
                SessionManager.Instance.OnLessonCompleted += _ => PlayLessonComplete();
            }
        }

        // ─────────────────── Public Play Methods ───────────────────

        public void PlayPinch() => PlaySFX(_pinchSound, _sfxVolume);
        public void PlayRelease() => PlaySFX(_releaseSound, _sfxVolume);
        public void PlaySnapSuccess() => PlaySFX(_snapSuccessSound, _sfxVolume);
        public void PlaySnapError() => PlaySFX(_snapErrorSound, _sfxVolume);
        public void PlayStepAdvance() => PlaySFX(_stepAdvanceSound, _sfxVolume);
        public void PlayLessonComplete() => PlaySFX(_lessonCompleteSound, _sfxVolume);
        public void PlayMistake() => PlaySFX(_mistakeSound, _sfxVolume);
        public void PlayButtonHover() => PlaySFX(_buttonHoverSound, _uiVolume);
        public void PlayButtonActivate() => PlaySFX(_buttonActivateSound, _uiVolume);
        public void PlayPanelSlide() => PlaySFX(_panelSlideSound, _uiVolume);

        /// <summary>
        /// Sets the master SFX volume at runtime.
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// Sets the UI sound volume at runtime.
        /// </summary>
        public void SetUIVolume(float volume)
        {
            _uiVolume = Mathf.Clamp01(volume);
        }

        // ─────────────────── Private ───────────────────

        private void PlaySFX(AudioClip clip, float volume)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip, volume);
            }
        }
    }
}
