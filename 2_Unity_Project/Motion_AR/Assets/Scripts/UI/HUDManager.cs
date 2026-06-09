using UnityEngine;
using MotionAR.Core;
using MotionAR.Data;
using MotionAR.Interaction;

namespace MotionAR.UI
{
    /// <summary>
    /// Central manager for all HUD UI components. Coordinates visibility of panels,
    /// connects gesture events to UI actions, and manages the overall HUD state.
    /// <para>
    /// This is the single entry point for enabling/disabling the entire HUD system.
    /// Attach to a root "HUD" GameObject in the Canvas.
    /// </para>
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField]
        [Tooltip("Left slide panel containing the ProgressTracker.")]
        private SlidePanel _progressPanel;

        [SerializeField]
        [Tooltip("Right slide panel containing lesson info.")]
        private SlidePanel _infoPanel;

        [Header("Components")]
        [SerializeField] private InstructionDisplay _instructionDisplay;
        [SerializeField] private ErrorOverlay _errorOverlay;
        [SerializeField] private ScoreDisplay _scoreDisplay;
        [SerializeField] private CursorEffects _cursorEffects;
        [SerializeField] private ObjectHighlight _objectHighlight;

        [Header("Gesture Integration")]
        [SerializeField]
        [Tooltip("GestureDetector for dynamic gesture events.")]
        private GestureDetector _gestureDetector;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Show progress panel automatically when a lesson starts.")]
        private bool _autoShowProgressOnLessonStart = true;

        private bool _isHUDActive = true;

        /// <summary>Whether the HUD is currently active and visible.</summary>
        public bool IsHUDActive => _isHUDActive;

        private void OnEnable()
        {
            // Connect gesture events to UI actions.
            if (_gestureDetector != null)
            {
                _gestureDetector.OnDynamicGestureDetected += HandleGesture;
            }

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnLessonStarted += HandleLessonStarted;
                SessionManager.Instance.OnLessonCompleted += HandleLessonCompleted;
            }
        }

        private void OnDisable()
        {
            if (_gestureDetector != null)
            {
                _gestureDetector.OnDynamicGestureDetected -= HandleGesture;
            }

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnLessonStarted -= HandleLessonStarted;
                SessionManager.Instance.OnLessonCompleted -= HandleLessonCompleted;
            }
        }

        /// <summary>
        /// Handles dynamic gestures for UI navigation.
        /// </summary>
        private void HandleGesture(GestureType gesture, float confidence)
        {
            switch (gesture)
            {
                case GestureType.SwipeLeft:
                    // Toggle left progress panel.
                    if (_progressPanel != null) _progressPanel.Toggle();
                    break;

                case GestureType.SwipeRight:
                    // Toggle right info panel.
                    if (_infoPanel != null) _infoPanel.Toggle();
                    break;

                case GestureType.OpenPalm:
                    // Show all panels briefly.
                    ShowAllPanels();
                    break;

                case GestureType.Fist:
                    // Hide all panels.
                    HideAllPanels();
                    break;
            }
        }

        /// <summary>
        /// Shows the progress panel when a lesson starts.
        /// </summary>
        private void HandleLessonStarted(LessonData lesson)
        {
            if (_autoShowProgressOnLessonStart && _progressPanel != null)
            {
                _progressPanel.Show();
            }
        }

        /// <summary>
        /// Hides panels and shows score when lesson completes.
        /// </summary>
        private void HandleLessonCompleted(float score)
        {
            HideAllPanels();
            // ScoreDisplay handles itself via SessionManager event.
        }

        /// <summary>
        /// Shows all side panels.
        /// </summary>
        public void ShowAllPanels()
        {
            if (_progressPanel != null) _progressPanel.Show();
            if (_infoPanel != null) _infoPanel.Show();
        }

        /// <summary>
        /// Hides all side panels.
        /// </summary>
        public void HideAllPanels()
        {
            if (_progressPanel != null) _progressPanel.Hide();
            if (_infoPanel != null) _infoPanel.Hide();
        }

        /// <summary>
        /// Enables or disables the entire HUD system.
        /// </summary>
        /// <param name="active">Whether the HUD should be active.</param>
        public void SetHUDActive(bool active)
        {
            _isHUDActive = active;
            gameObject.SetActive(active);
        }
    }
}
