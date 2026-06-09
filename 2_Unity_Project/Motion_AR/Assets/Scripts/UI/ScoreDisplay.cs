using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using MotionAR.Core;

namespace MotionAR.UI
{
    /// <summary>
    /// Displays the final score when a lesson is completed.
    /// Shows score, time elapsed, mistakes count with celebration animation.
    /// <para>
    /// Design: Glassmorphism panel with neon borders and grade letter.
    /// </para>
    /// </summary>
    public class ScoreDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _scorePanel;
        [SerializeField] private TextMeshProUGUI _scoreValueText;
        [SerializeField] private TextMeshProUGUI _gradeLetterText;
        [SerializeField] private TextMeshProUGUI _timeText;
        [SerializeField] private TextMeshProUGUI _mistakeText;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _menuButton;

        [Header("Grade Colors")]
        [SerializeField] private Color _gradeS = new Color(1f, 0.84f, 0f, 1f);        // Gold
        [SerializeField] private Color _gradeA = new Color(0.22f, 1f, 0.08f, 1f);     // Neon green
        [SerializeField] private Color _gradeB = new Color(0f, 1f, 1f, 1f);            // Cyan
        [SerializeField] private Color _gradeC = new Color(1f, 0.65f, 0f, 1f);         // Orange
        [SerializeField] private Color _gradeF = new Color(1f, 0.027f, 0.227f, 1f);   // Neon red

        [Header("Animation")]
        [SerializeField] private float _popInDuration = 0.4f;
        [SerializeField] private float _scoreCountDuration = 1.5f;

        private CancellationTokenSource _animCts;

        private void Awake()
        {
            if (_scorePanel != null)
            {
                _scorePanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnLessonCompleted += ShowScore;
            }
        }

        private void OnDisable()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnLessonCompleted -= ShowScore;
            }

            CancelAnimation();
        }

        /// <summary>
        /// Shows the score panel with animated reveal.
        /// </summary>
        public void ShowScore(float score)
        {
            CancelAnimation();
            _animCts = new CancellationTokenSource();
            AnimateScoreReveal(score, _animCts.Token).Forget();
        }

        /// <summary>
        /// Animates the score panel appearance with pop-in and counting effect.
        /// </summary>
        private async UniTaskVoid AnimateScoreReveal(float finalScore, CancellationToken ct)
        {
            if (_scorePanel == null) return;

            // Determine grade and message.
            (string grade, Color color, string message) = GetGradeInfo(finalScore);

            // Set initial state.
            _scorePanel.SetActive(true);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.transform.localScale = Vector3.one * 0.5f;
            }

            // Pop-in animation.
            float elapsed = 0f;
            while (elapsed < _popInDuration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = elapsed / _popInDuration;
                // Ease out back for bouncy pop effect.
                float eased = 1f + 2.7f * Mathf.Pow(t - 1f, 3f) + 1.7f * Mathf.Pow(t - 1f, 2f);

                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = Mathf.Clamp01(t * 2f); // Fade in faster.
                    _canvasGroup.transform.localScale = Vector3.one * eased;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.transform.localScale = Vector3.one;
            }

            // Set grade and message.
            if (_gradeLetterText != null)
            {
                _gradeLetterText.text = grade;
                _gradeLetterText.color = color;
            }

            if (_messageText != null)
            {
                _messageText.text = message;
            }

            // Count-up score animation.
            elapsed = 0f;
            while (elapsed < _scoreCountDuration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _scoreCountDuration);
                float displayScore = Mathf.Lerp(0f, finalScore, t);

                if (_scoreValueText != null)
                {
                    _scoreValueText.text = $"{displayScore:F0}";
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (_scoreValueText != null)
            {
                _scoreValueText.text = $"{finalScore:F0}";
            }

            // Set stats.
            if (_timeText != null && SessionManager.Instance != null)
            {
                float time = SessionManager.Instance.ElapsedTime;
                _timeText.text = $"Thời gian: {FormatTime(time)}";
            }

            if (_mistakeText != null && SessionManager.Instance != null)
            {
                int mistakes = SessionManager.Instance.MistakeCount;
                _mistakeText.text = $"Lỗi: {mistakes}";
            }
        }

        /// <summary>
        /// Returns grade letter, color, and congratulation message based on score.
        /// </summary>
        private (string grade, Color color, string message) GetGradeInfo(float score)
        {
            if (score >= 95f) return ("S", _gradeS, "🏆 Xuất sắc! Hoàn hảo!");
            if (score >= 80f) return ("A", _gradeA, "⭐ Rất tốt! Tiếp tục phát huy!");
            if (score >= 60f) return ("B", _gradeB, "👍 Tốt! Còn có thể cải thiện.");
            if (score >= 40f) return ("C", _gradeC, "📝 Cần luyện tập thêm.");
            return ("F", _gradeF, "❌ Hãy thử lại!");
        }

        /// <summary>
        /// Formats seconds into mm:ss string.
        /// </summary>
        private string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:D2}:{s:D2}";
        }

        /// <summary>
        /// Hides the score panel.
        /// </summary>
        public void HideScore()
        {
            if (_scorePanel != null)
            {
                _scorePanel.SetActive(false);
            }
        }

        private void CancelAnimation()
        {
            if (_animCts != null)
            {
                _animCts.Cancel();
                _animCts.Dispose();
                _animCts = null;
            }
        }
    }
}
