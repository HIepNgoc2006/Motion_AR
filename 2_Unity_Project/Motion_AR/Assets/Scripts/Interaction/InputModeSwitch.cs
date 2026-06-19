using UnityEngine;

namespace MotionAR.Interaction
{
    /// <summary>
    /// Runtime switch between Mock (mouse) and MediaPipe (webcam) hand tracking input.
    /// <para>
    /// Reads the current <see cref="MotionAR.Core.SettingsManager.CurrentInputMode"/>
    /// and activates the corresponding provider. Supports hot-switching via
    /// <see cref="SetMode"/> without restarting the scene.
    /// </para>
    /// </summary>
    public class InputModeSwitch : MonoBehaviour
    {
        [Header("Providers")]
        [SerializeField]
        [Tooltip("Mock provider for mouse-based testing.")]
        private MockHandTrackingProvider _mockProvider;

        [SerializeField]
        [Tooltip("MediaPipe UDP provider for real hand tracking.")]
        private MediaPipeUDPProvider _mediaPipeProvider;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Default input mode to use on startup.")]
        private InputMode _defaultMode = InputMode.Mock;

        private InputMode _currentMode;

        /// <summary>
        /// The currently active input mode.
        /// </summary>
        public InputMode CurrentMode => _currentMode;

        /// <summary>
        /// Input modes for the application.
        /// </summary>
        public enum InputMode
        {
            /// <summary>Mouse-based mock input for editor testing.</summary>
            Mock = 0,
            /// <summary>Real hand tracking via MediaPipe Python bridge.</summary>
            MediaPipe = 1
        }

        private void Start()
        {
            // Check SettingsManager for saved preference.
            InputMode startMode = _defaultMode;

            if (MotionAR.Core.SettingsManager.Instance != null)
            {
                string savedMode = MotionAR.Core.SettingsManager.Instance.GetString("InputMode", "Mock");
                if (System.Enum.TryParse<InputMode>(savedMode, out InputMode parsed))
                {
                    startMode = parsed;
                }
            }

            SetMode(startMode);
        }

        /// <summary>
        /// Switches the active input mode at runtime.
        /// Stops the old provider and starts the new one.
        /// </summary>
        /// <param name="mode">The input mode to activate.</param>
        public void SetMode(InputMode mode)
        {
            _currentMode = mode;

            switch (mode)
            {
                case InputMode.Mock:
                    ActivateMock();
                    break;

                case InputMode.MediaPipe:
                    ActivateMediaPipe();
                    break;
            }

            // Save preference.
            if (MotionAR.Core.SettingsManager.Instance != null)
            {
                MotionAR.Core.SettingsManager.Instance.SetString("InputMode", mode.ToString());
            }

            Debug.Log($"[InputModeSwitch] Switched to: {mode}");
        }

        /// <summary>
        /// Toggles between Mock and MediaPipe modes.
        /// </summary>
        public void ToggleMode()
        {
            SetMode(_currentMode == InputMode.Mock ? InputMode.MediaPipe : InputMode.Mock);
        }

        private void ActivateMock()
        {
            // Enable mock, disable MediaPipe.
            if (_mockProvider != null)
            {
                _mockProvider.enabled = true;
                _mockProvider.StartTracking();
            }

            if (_mediaPipeProvider != null)
            {
                _mediaPipeProvider.StopTracking();
                _mediaPipeProvider.enabled = false;
            }
        }

        private void ActivateMediaPipe()
        {
            // Enable MediaPipe, disable mock.
            if (_mediaPipeProvider != null)
            {
                _mediaPipeProvider.enabled = true;
                _mediaPipeProvider.StartTracking();
            }

            if (_mockProvider != null)
            {
                _mockProvider.StopTracking();
                _mockProvider.enabled = false;
            }
        }
    }
}
