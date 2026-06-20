using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using MotionAR.Data;
using MotionAR.UI;

namespace MotionAR.Core
{
    /// <summary>
    /// Manages the overall application navigation flow:
    /// Login → LessonSelect → Training → Score → (Back to LessonSelect).
    /// <para>
    /// Singleton that persists across scenes via <c>DontDestroyOnLoad</c>.
    /// Coordinates transitions between UI panels without requiring multiple Unity scenes.
    /// All panels are children of a single-scene Canvas, toggled on/off by this controller.
    /// </para>
    /// </summary>
    public class AppFlowManager : MonoBehaviour
    {
        // ────────────────────── Singleton ──────────────────────

        /// <summary>Global singleton instance.</summary>
        public static AppFlowManager Instance { get; private set; }

        // ────────────────────── State ──────────────────────

        /// <summary>
        /// The current phase of the application flow.
        /// </summary>
        public AppPhase CurrentPhase { get; private set; } = AppPhase.Login;

        // ────────────────────── Panel References ──────────────────────

        [Header("Panel References")]
        [SerializeField]
        [Tooltip("Root GameObject of the Login panel.")]
        private GameObject _loginPanel;

        [SerializeField]
        [Tooltip("Root GameObject of the Lesson Select panel.")]
        private GameObject _lessonSelectPanel;

        [SerializeField]
        [Tooltip("Root GameObject of the Training HUD (in-session UI).")]
        private GameObject _trainingHUD;

        [SerializeField]
        [Tooltip("Root GameObject of the Score/Results panel.")]
        private GameObject _scorePanel;

        [SerializeField]
        [Tooltip("Root GameObject of the Session History panel.")]
        private GameObject _historyPanel;

        [SerializeField]
        [Tooltip("Root GameObject of the Settings panel.")]
        private GameObject _settingsPanel;

        [Header("3D Workspace")]
        [SerializeField]
        [Tooltip("Root GameObject containing all 3D parts and snap zones.")]
        private GameObject _workspaceRoot;

        [Header("Transition")]
        [SerializeField]
        [Tooltip("Canvas Group for fade transitions between panels.")]
        private CanvasGroup _transitionOverlay;

        [SerializeField]
        [Tooltip("Duration of fade transitions in seconds.")]
        private float _fadeDuration = 0.3f;

        private bool _isTransitioning;
        private CancellationTokenSource _transitionCts;

        // ────────────────────── Events ──────────────────────

        /// <summary>
        /// Fired when the app phase changes. Subscribers receive the new phase.
        /// </summary>
        public event System.Action<AppPhase> OnPhaseChanged;

        // ────────────────────── Unity Lifecycle ──────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Initialize to Login phase.
            TransitionTo(AppPhase.Login);
        }

        // ────────────────────── Public Navigation Methods ──────────────────────

        /// <summary>
        /// Transitions to the Login screen.
        /// Called at startup or when the user logs out.
        /// </summary>
        public void ShowLogin()
        {
            TransitionTo(AppPhase.Login);
        }

        /// <summary>
        /// Transitions to the Lesson Select menu after successful login.
        /// </summary>
        public void ShowLessonSelect()
        {
            TransitionTo(AppPhase.LessonSelect);
        }

        /// <summary>
        /// Transitions to the Training phase with the given lesson.
        /// Starts the lesson in SessionManager and shows the 3D workspace.
        /// </summary>
        /// <param name="lesson">The LessonData to load for training.</param>
        public void StartTraining(LessonData lesson)
        {
            if (lesson == null)
            {
                Debug.LogError("[AppFlowManager] Cannot start training with null lesson.");
                return;
            }

            // Start lesson in SessionManager.
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.StartLesson(lesson);
            }

            TransitionTo(AppPhase.Training);
        }

        /// <summary>
        /// Transitions to the Score/Results screen.
        /// Called when a lesson is completed.
        /// </summary>
        public void ShowScore()
        {
            TransitionTo(AppPhase.Score);
        }

        /// <summary>
        /// Transitions to the Session History panel.
        /// </summary>
        public void ShowHistory()
        {
            TransitionTo(AppPhase.History);
        }

        /// <summary>
        /// Transitions to the Settings panel.
        /// </summary>
        public void ShowSettings()
        {
            TransitionTo(AppPhase.Settings);
        }

        /// <summary>
        /// Returns to the previous logical phase.
        /// Score/History/Settings → LessonSelect, Training → LessonSelect (with confirmation).
        /// </summary>
        public void GoBack()
        {
            switch (CurrentPhase)
            {
                case AppPhase.Score:
                case AppPhase.History:
                case AppPhase.Settings:
                    TransitionTo(AppPhase.LessonSelect);
                    break;

                case AppPhase.Training:
                    // Show confirmation before aborting training.
                    ConfirmExitTrainingAsync().Forget();
                    break;

                case AppPhase.LessonSelect:
                    TransitionTo(AppPhase.Login);
                    break;
            }
        }

        // ────────────────────── Private ──────────────────────

        /// <summary>
        /// Core transition method. Hides all panels, then shows the target panel.
        /// Uses optional fade overlay if assigned.
        /// </summary>
        private void TransitionTo(AppPhase targetPhase)
        {
            if (_transitionOverlay != null)
            {
                FadeTransitionAsync(targetPhase).Forget();
            }
            else
            {
                ApplyPhase(targetPhase);
            }
        }

        /// <summary>
        /// Performs a smooth fade transition between panels.
        /// </summary>
        private async UniTaskVoid FadeTransitionAsync(AppPhase targetPhase)
        {
            // Cancel any in-flight transition to prevent stacking.
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            _transitionCts = new CancellationTokenSource();
            var ct = _transitionCts.Token;

            if (_isTransitioning) return;
            _isTransitioning = true;

            try
            {
                // Fade to black.
                if (_transitionOverlay != null)
                {
                    _transitionOverlay.gameObject.SetActive(true);
                    _transitionOverlay.blocksRaycasts = true;

                    float elapsed = 0f;
                    while (elapsed < _fadeDuration)
                    {
                        ct.ThrowIfCancellationRequested();
                        elapsed += Time.unscaledDeltaTime;
                        _transitionOverlay.alpha = Mathf.Clamp01(elapsed / _fadeDuration);
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }
                    _transitionOverlay.alpha = 1f;
                }

                // Apply the phase change while screen is black.
                ApplyPhase(targetPhase);

                // Fade back in.
                if (_transitionOverlay != null)
                {
                    float elapsed = 0f;
                    while (elapsed < _fadeDuration)
                    {
                        ct.ThrowIfCancellationRequested();
                        elapsed += Time.unscaledDeltaTime;
                        _transitionOverlay.alpha = 1f - Mathf.Clamp01(elapsed / _fadeDuration);
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }
                    _transitionOverlay.alpha = 0f;
                    _transitionOverlay.blocksRaycasts = false;
                    _transitionOverlay.gameObject.SetActive(false);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Transition was cancelled by a newer transition — apply immediately.
                ApplyPhase(targetPhase);
                if (_transitionOverlay != null)
                {
                    _transitionOverlay.alpha = 0f;
                    _transitionOverlay.blocksRaycasts = false;
                    _transitionOverlay.gameObject.SetActive(false);
                }
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// Applies the phase by toggling panel visibility.
        /// </summary>
        private void ApplyPhase(AppPhase phase)
        {
            CurrentPhase = phase;

            // Hide all panels.
            SetActive(_loginPanel, false);
            SetActive(_lessonSelectPanel, false);
            SetActive(_trainingHUD, false);
            SetActive(_scorePanel, false);
            SetActive(_historyPanel, false);
            SetActive(_settingsPanel, false);
            SetActive(_workspaceRoot, false);

            // Show target panel.
            switch (phase)
            {
                case AppPhase.Login:
                    SetActive(_loginPanel, true);
                    break;

                case AppPhase.LessonSelect:
                    SetActive(_lessonSelectPanel, true);
                    break;

                case AppPhase.Training:
                    SetActive(_trainingHUD, true);
                    SetActive(_workspaceRoot, true);
                    break;

                case AppPhase.Score:
                    SetActive(_scorePanel, true);
                    break;

                case AppPhase.History:
                    SetActive(_historyPanel, true);
                    break;

                case AppPhase.Settings:
                    SetActive(_settingsPanel, true);
                    break;
            }

            OnPhaseChanged?.Invoke(phase);
            Debug.Log($"[AppFlowManager] Transitioned to: {phase}");
        }

        /// <summary>
        /// Safely sets a GameObject active/inactive, handling null references.
        /// </summary>
        private static void SetActive(GameObject go, bool active)
        {
            if (go != null)
            {
                go.SetActive(active);
            }
        }

        /// <summary>
        /// Shows a confirmation dialog before exiting training.
        /// Prevents accidental progress loss.
        /// </summary>
        private async UniTaskVoid ConfirmExitTrainingAsync()
        {
            if (ConfirmDialog.Instance != null)
            {
                bool confirmed = await ConfirmDialog.Instance.ShowAsync(
                    "Thoát bài học?",
                    "Tiến độ hiện tại sẽ không được lưu.\nBạn có chắc chắn muốn thoát?",
                    "Thoát",
                    "Tiếp tục học");

                if (!confirmed) return;
            }

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.ResetSession();
            }
            TransitionTo(AppPhase.LessonSelect);
        }
    }

    /// <summary>
    /// Enumeration of application navigation phases.
    /// </summary>
    public enum AppPhase
    {
        /// <summary>User login screen.</summary>
        Login,
        /// <summary>Lesson selection menu.</summary>
        LessonSelect,
        /// <summary>Active training session.</summary>
        Training,
        /// <summary>Score/results display.</summary>
        Score,
        /// <summary>Session history panel.</summary>
        History,
        /// <summary>Application settings.</summary>
        Settings
    }
}
