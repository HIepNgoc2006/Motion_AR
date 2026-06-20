using System;
using UnityEngine;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Threading;
using MotionAR.Core;
using MotionAR.Data;

namespace MotionAR.UI
{
    /// <summary>
    /// Displays the current step instruction in a holographic panel.
    /// Automatically updates when the session advances to a new step.
    /// <para>
    /// Design: Sci-Fi style panel with fade-in animation and step counter.
    /// </para>
    /// </summary>
    public class InstructionDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        [Tooltip("TextMeshPro text for the instruction content.")]
        private TextMeshProUGUI _instructionText;

        [SerializeField]
        [Tooltip("TextMeshPro text for the step counter (e.g., 'Step 2 / 5').")]
        private TextMeshProUGUI _stepCounterText;

        [SerializeField]
        [Tooltip("TextMeshPro text for the lesson title.")]
        private TextMeshProUGUI _lessonTitleText;

        [SerializeField]
        [Tooltip("CanvasGroup for fade animations.")]
        private CanvasGroup _canvasGroup;

        [Header("Animation")]
        [SerializeField] private float _fadeInDuration = 0.3f;
        [SerializeField] private float _fadeOutDuration = 0.15f;

        [Header("Colors")]
        [SerializeField] private Color _normalStepColor = new Color(0f, 1f, 1f, 1f);     // Cyan
        [SerializeField] private Color _completedColor = new Color(0.22f, 1f, 0.08f, 1f); // Neon green

        private CancellationTokenSource _fadeCts;

        private void Start()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnStepChanged += HandleStepChanged;
                SessionManager.Instance.OnLessonStarted += HandleLessonStarted;
                SessionManager.Instance.OnLessonCompleted += HandleLessonCompleted;
            }
        }

        private void OnDisable()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnStepChanged -= HandleStepChanged;
                SessionManager.Instance.OnLessonStarted -= HandleLessonStarted;
                SessionManager.Instance.OnLessonCompleted -= HandleLessonCompleted;
            }

            CancelFade();
        }

        /// <summary>
        /// Initializes display when a new lesson starts.
        /// </summary>
        private void HandleLessonStarted(LessonData lesson)
        {
            if (_lessonTitleText != null)
            {
                _lessonTitleText.text = lesson.lessonTitle;
            }
        }

        /// <summary>
        /// Updates the instruction display with fade animation when step changes.
        /// </summary>
        private void HandleStepChanged(StepData step)
        {
            if (step == null) return;

            CancelFade();
            _fadeCts = new CancellationTokenSource();
            AnimateStepTransition(step, _fadeCts.Token).Forget();
        }

        /// <summary>
        /// Shows completion message when lesson is finished.
        /// </summary>
        private void HandleLessonCompleted(float score)
        {
            CancelFade();
            _fadeCts = new CancellationTokenSource();
            AnimateCompletion(score, _fadeCts.Token).Forget();
        }

        /// <summary>
        /// Fades out old instruction, updates text, fades in new instruction.
        /// </summary>
        private async UniTaskVoid AnimateStepTransition(StepData step, CancellationToken ct)
        {
            // Fade out.
            if (_canvasGroup != null)
            {
                await FadeCanvasGroup(_canvasGroup, _canvasGroup.alpha, 0f, _fadeOutDuration, ct);
            }

            // Update text content.
            if (_instructionText != null)
            {
                _instructionText.text = step.instructionText;
                _instructionText.color = _normalStepColor;
            }

            if (_stepCounterText != null && SessionManager.Instance != null)
            {
                int current = SessionManager.Instance.CurrentStepIndex + 1;
                int total = SessionManager.Instance.CurrentLesson.TotalSteps;
                _stepCounterText.text = $"Bước {current} / {total}";
            }

            // Fade in.
            if (_canvasGroup != null)
            {
                await FadeCanvasGroup(_canvasGroup, 0f, 1f, _fadeInDuration, ct);
            }
        }

        /// <summary>
        /// Animates the completion message with neon green color.
        /// </summary>
        private async UniTaskVoid AnimateCompletion(float score, CancellationToken ct)
        {
            if (_canvasGroup != null)
            {
                await FadeCanvasGroup(_canvasGroup, _canvasGroup.alpha, 0f, _fadeOutDuration, ct);
            }

            if (_instructionText != null)
            {
                _instructionText.text = $"✅ HOÀN THÀNH!\nĐiểm: {score:F0} / 100";
                _instructionText.color = _completedColor;
            }

            if (_stepCounterText != null)
            {
                _stepCounterText.text = "Hoàn tất";
            }

            if (_canvasGroup != null)
            {
                await FadeCanvasGroup(_canvasGroup, 0f, 1f, _fadeInDuration, ct);
            }
        }

        /// <summary>
        /// Smoothly interpolates a CanvasGroup's alpha over time using UniTask.
        /// </summary>
        private async UniTask FadeCanvasGroup(CanvasGroup cg, float from, float to,
            float duration, CancellationToken ct)
        {
            float elapsed = 0f;
            cg.alpha = from;

            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            cg.alpha = to;
        }

        private void CancelFade()
        {
            if (_fadeCts != null)
            {
                _fadeCts.Cancel();
                _fadeCts.Dispose();
                _fadeCts = null;
            }
        }
    }
}
