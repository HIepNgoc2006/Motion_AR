using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MotionAR.Core;
using MotionAR.Data;

namespace MotionAR.UI
{
    /// <summary>
    /// Displays lesson progress as a step-by-step checklist with visual indicators.
    /// Each step shows its status: pending, current, or completed.
    /// <para>
    /// Attaches to the left SlidePanel and auto-updates via SessionManager events.
    /// </para>
    /// </summary>
    public class ProgressTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Parent transform where step items will be instantiated.")]
        private Transform _stepsContainer;

        [SerializeField]
        [Tooltip("Prefab for each step item in the progress list.")]
        private GameObject _stepItemPrefab;

        [SerializeField]
        [Tooltip("Progress bar fill image.")]
        private Image _progressBarFill;

        [SerializeField]
        [Tooltip("Text showing 'Step X/Y'.")]
        private TextMeshProUGUI _progressText;

        [Header("Colors")]
        [SerializeField] private Color _pendingColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        [SerializeField] private Color _currentColor = new Color(0f, 1f, 1f, 1f);         // Cyan
        [SerializeField] private Color _completedColor = new Color(0.22f, 1f, 0.08f, 1f); // Neon green

        private StepItemUI[] _stepItems;
        private int _totalSteps;

        private void Start()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnLessonStarted += HandleLessonStarted;
                SessionManager.Instance.OnStepChanged += HandleStepChanged;
                SessionManager.Instance.OnLessonCompleted += HandleLessonCompleted;
            }
        }

        private void OnDisable()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnLessonStarted -= HandleLessonStarted;
                SessionManager.Instance.OnStepChanged -= HandleStepChanged;
                SessionManager.Instance.OnLessonCompleted -= HandleLessonCompleted;
            }
        }

        /// <summary>
        /// Builds the step list when a lesson starts.
        /// </summary>
        private void HandleLessonStarted(LessonData lesson)
        {
            ClearSteps();

            _totalSteps = lesson.TotalSteps;
            _stepItems = new StepItemUI[_totalSteps];

            for (int i = 0; i < _totalSteps; i++)
            {
                StepData step = lesson.GetStep(i);
                if (step == null) continue;

                GameObject item;
                if (_stepItemPrefab != null && _stepsContainer != null)
                {
                    item = Instantiate(_stepItemPrefab, _stepsContainer);
                }
                else
                {
                    // Create a simple text element if no prefab is assigned.
                    item = new GameObject($"Step_{i}");
                    if (_stepsContainer != null) item.transform.SetParent(_stepsContainer, false);
                    item.AddComponent<RectTransform>();
                }

                StepItemUI itemUI = item.GetComponent<StepItemUI>();
                if (itemUI == null) itemUI = item.AddComponent<StepItemUI>();

                itemUI.Initialize(i, step.instructionText, _pendingColor, _currentColor, _completedColor);
                _stepItems[i] = itemUI;
            }

            // Highlight first step.
            if (_stepItems.Length > 0)
            {
                _stepItems[0].SetState(StepItemUI.StepState.Current);
            }

            UpdateProgressBar(0);
        }

        /// <summary>
        /// Updates step states when the session advances.
        /// </summary>
        private void HandleStepChanged(StepData newStep)
        {
            if (_stepItems == null) return;

            int currentIndex = SessionManager.Instance.CurrentStepIndex;

            for (int i = 0; i < _stepItems.Length; i++)
            {
                if (i < currentIndex)
                    _stepItems[i].SetState(StepItemUI.StepState.Completed);
                else if (i == currentIndex)
                    _stepItems[i].SetState(StepItemUI.StepState.Current);
                else
                    _stepItems[i].SetState(StepItemUI.StepState.Pending);
            }

            UpdateProgressBar(currentIndex);
        }

        /// <summary>
        /// Marks all steps as completed when the lesson finishes.
        /// </summary>
        private void HandleLessonCompleted(float score)
        {
            if (_stepItems == null) return;

            for (int i = 0; i < _stepItems.Length; i++)
            {
                _stepItems[i].SetState(StepItemUI.StepState.Completed);
            }

            UpdateProgressBar(_totalSteps);
        }

        /// <summary>
        /// Updates the progress bar fill and text.
        /// </summary>
        private void UpdateProgressBar(int completedSteps)
        {
            if (_progressBarFill != null)
            {
                float progress = _totalSteps > 0
                    ? (float)completedSteps / _totalSteps
                    : 0f;
                _progressBarFill.fillAmount = progress;
                _progressBarFill.color = Color.Lerp(_currentColor, _completedColor, progress);
            }

            if (_progressText != null)
            {
                _progressText.text = $"{completedSteps} / {_totalSteps}";
            }
        }

        /// <summary>
        /// Destroys all child step items.
        /// </summary>
        private void ClearSteps()
        {
            if (_stepsContainer != null)
            {
                for (int i = _stepsContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(_stepsContainer.GetChild(i).gameObject);
                }
            }

            _stepItems = null;
        }
    }

    /// <summary>
    /// Individual step item in the ProgressTracker list.
    /// Shows step number, instruction text, and state indicator.
    /// </summary>
    public class StepItemUI : MonoBehaviour
    {
        public enum StepState { Pending, Current, Completed }

        private TextMeshProUGUI _text;
        private Image _indicator;
        private int _stepIndex;
        private string _instruction;
        private Color _pendingColor, _currentColor, _completedColor;

        /// <summary>
        /// Initializes the step item with index, instruction, and colors.
        /// </summary>
        public void Initialize(int index, string instruction,
            Color pending, Color current, Color completed)
        {
            _stepIndex = index;
            _instruction = instruction;
            _pendingColor = pending;
            _currentColor = current;
            _completedColor = completed;

            // Try to find text component.
            _text = GetComponentInChildren<TextMeshProUGUI>();
            if (_text == null)
            {
                // Create one if not found.
                _text = gameObject.AddComponent<TextMeshProUGUI>();
                _text.fontSize = 14;
                _text.alignment = TextAlignmentOptions.Left;
            }

            _indicator = GetComponentInChildren<Image>();

            SetState(StepState.Pending);
        }

        /// <summary>
        /// Updates the visual state of this step item.
        /// </summary>
        public void SetState(StepState state)
        {
            if (_text == null) return;

            string prefix = state switch
            {
                StepState.Pending => "○",
                StepState.Current => "▶",
                StepState.Completed => "✓",
                _ => "○"
            };

            Color color = state switch
            {
                StepState.Pending => _pendingColor,
                StepState.Current => _currentColor,
                StepState.Completed => _completedColor,
                _ => _pendingColor
            };

            _text.text = $"{prefix} Bước {_stepIndex + 1}: {_instruction}";
            _text.color = color;

            if (_indicator != null)
            {
                _indicator.color = color;
            }
        }
    }
}
