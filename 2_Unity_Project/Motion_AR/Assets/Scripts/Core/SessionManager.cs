using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MotionAR.Data;
using MotionAR.Database;

namespace MotionAR.Core
{
    /// <summary>
    /// Singleton MonoBehaviour that manages the in-memory training session state.
    /// Tracks the active lesson, current step, mistake count, elapsed time,
    /// and persists completed session results to the local SQLite database.
    /// Uses the Lazy Singleton pattern with <c>DontDestroyOnLoad</c>.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        // ────────────────────────── Singleton ──────────────────────────

        /// <summary>
        /// The global singleton instance of <see cref="SessionManager"/>.
        /// Guaranteed to survive scene transitions via <c>DontDestroyOnLoad</c>.
        /// </summary>
        public static SessionManager Instance { get; private set; }

        // ────────────────────── Session State (private) ──────────────────────

        private LessonData _currentLesson;
        private int _currentStepIndex;
        private int _mistakeCount;
        private float _sessionStartTime;
        private string _currentUserId;
        private bool _isLessonActive;

        // ────────────────────────── Public Properties ──────────────────────────

        /// <summary>
        /// The <see cref="LessonData"/> ScriptableObject that is currently loaded.
        /// <c>null</c> when no lesson is active.
        /// </summary>
        public LessonData CurrentLesson => _currentLesson;

        /// <summary>
        /// Zero-based index of the step the learner is currently on.
        /// </summary>
        public int CurrentStepIndex => _currentStepIndex;

        /// <summary>
        /// Total number of mistakes recorded during the current session.
        /// </summary>
        public int MistakeCount => _mistakeCount;

        /// <summary>
        /// Whether a lesson is currently in progress.
        /// </summary>
        public bool IsLessonActive => _isLessonActive;

        /// <summary>
        /// The unique identifier of the currently logged-in user.
        /// Must be set before starting a lesson so that results can be saved.
        /// </summary>
        public string CurrentUserId
        {
            get => _currentUserId;
            set => _currentUserId = value;
        }

        /// <summary>
        /// Wall-clock seconds elapsed since the current lesson started.
        /// Returns <c>0</c> when no lesson is active.
        /// </summary>
        public float ElapsedTime => _isLessonActive ? Time.time - _sessionStartTime : 0f;

        // ────────────────────────── Events ──────────────────────────

        /// <summary>
        /// Raised every time the session advances to a new step.
        /// Subscribers receive the <see cref="StepData"/> for the new step.
        /// </summary>
        public event Action<StepData> OnStepChanged;

        /// <summary>
        /// Raised each time a mistake is recorded via <see cref="RecordMistake"/>.
        /// </summary>
        public event Action OnMistakeRecorded;

        /// <summary>
        /// Raised when the lesson is completed.
        /// The <c>float</c> payload is the final score (0–100).
        /// </summary>
        public event Action<float> OnLessonCompleted;

        /// <summary>
        /// Raised when a new lesson begins via <see cref="StartLesson"/>.
        /// Subscribers receive the <see cref="LessonData"/> that was started.
        /// </summary>
        public event Action<LessonData> OnLessonStarted;

        // ────────────────────────── Unity Lifecycle ──────────────────────────

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

        // ────────────────────────── Public Methods ──────────────────────────

        /// <summary>
        /// Begins a new training session for the supplied lesson.
        /// Resets all counters and fires <see cref="OnLessonStarted"/> and
        /// <see cref="OnStepChanged"/> for the first step.
        /// </summary>
        /// <param name="lesson">
        /// The <see cref="LessonData"/> ScriptableObject to load.
        /// Must not be <c>null</c> and must contain at least one step.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="lesson"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="lesson"/> contains no assembly steps.
        /// </exception>
        public void StartLesson(LessonData lesson)
        {
            if (lesson == null)
            {
                throw new ArgumentNullException(nameof(lesson), "[SessionManager] Cannot start a null lesson.");
            }

            if (lesson.TotalSteps <= 0)
            {
                throw new ArgumentException("[SessionManager] Lesson has no steps.", nameof(lesson));
            }

            _currentLesson = lesson;
            _currentStepIndex = 0;
            _mistakeCount = 0;
            _sessionStartTime = Time.time;
            _isLessonActive = true;

            OnLessonStarted?.Invoke(lesson);
            OnStepChanged?.Invoke(GetCurrentStep());

            Debug.Log($"[SessionManager] Started lesson: {lesson.lessonTitle}");
        }

        /// <summary>
        /// Returns the <see cref="StepData"/> for the current step index,
        /// or <c>null</c> if no lesson is loaded.
        /// </summary>
        /// <returns>The current <see cref="StepData"/>, or <c>null</c>.</returns>
        public StepData GetCurrentStep()
        {
            return _currentLesson?.GetStep(_currentStepIndex);
        }

        /// <summary>
        /// Advances the session to the next step.
        /// If the learner has completed all steps the lesson is finalised
        /// via <see cref="CompleteLessonInternal"/>.
        /// Does nothing when no lesson is active.
        /// </summary>
        public void AdvanceStep()
        {
            if (!_isLessonActive)
            {
                return;
            }

            _currentStepIndex++;

            if (_currentStepIndex >= _currentLesson.TotalSteps)
            {
                CompleteLessonInternal();
            }
            else
            {
                OnStepChanged?.Invoke(GetCurrentStep());
                Debug.Log($"[SessionManager] Advanced to step {_currentStepIndex}");
            }
        }

        /// <summary>
        /// Increments the mistake counter and fires <see cref="OnMistakeRecorded"/>.
        /// Does nothing when no lesson is active.
        /// </summary>
        public void RecordMistake()
        {
            if (!_isLessonActive)
            {
                return;
            }

            _mistakeCount++;
            OnMistakeRecorded?.Invoke();
            Debug.Log($"[SessionManager] Mistake recorded. Total: {_mistakeCount}");
        }

        /// <summary>
        /// Resets all session state to defaults, clearing the loaded lesson.
        /// Does <b>not</b> save anything to the database — call this only
        /// when you want to discard the current session entirely.
        /// </summary>
        public void ResetSession()
        {
            _currentLesson = null;
            _currentStepIndex = 0;
            _mistakeCount = 0;
            _isLessonActive = false;
        }

        // ────────────────────────── Private Methods ──────────────────────────

        /// <summary>
        /// Finalises the current lesson: calculates the score, fires
        /// <see cref="OnLessonCompleted"/>, and kicks off a fire-and-forget
        /// database save so the main thread is never blocked.
        /// </summary>
        private void CompleteLessonInternal()
        {
            _isLessonActive = false;

            float score = Mathf.Max(0f, 100f - _mistakeCount * 10f);
            float totalTime = Time.time - _sessionStartTime;

            // Capture values before async save to avoid race with ResetSession().
            string userId = _currentUserId;
            string lessonId = _currentLesson?.lessonID ?? string.Empty;

            OnLessonCompleted?.Invoke(score);
            Debug.Log($"[SessionManager] Lesson complete! Score: {score}, Time: {totalTime:F1}s");

            // Fire-and-forget async database persistence (non-blocking).
            SaveSessionToDatabase(userId, lessonId, score, totalTime).Forget();
        }

        /// <summary>
        /// Persists the completed session row into the local SQLite database
        /// via <see cref="SQLiteConnector"/>. Runs asynchronously and is
        /// cancelled automatically when this <see cref="GameObject"/> is
        /// destroyed (uses <c>destroyCancellationToken</c>).
        /// </summary>
        /// <param name="score">Final calculated score (0–100).</param>
        /// <param name="totalTime">Total elapsed time in seconds.</param>
        private async UniTaskVoid SaveSessionToDatabase(
            string userId, string lessonId, float score, float totalTime)
        {
            try
            {
                SQLiteConnector db = SQLiteConnector.Instance;
                await db.InsertSessionAsync(
                    userId,
                    lessonId,
                    totalTime,
                    _mistakeCount,
                    score,
                    destroyCancellationToken
                );

                Debug.Log("[SessionManager] Session saved to database.");
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[SessionManager] Database save was cancelled (object destroyed).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionManager] Failed to save session: {ex.Message}");
            }
        }
    }
}
