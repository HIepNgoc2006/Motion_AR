using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace MotionAR.UI
{
    /// <summary>
    /// Sliding panel that animates in/out from screen edges.
    /// Used for side panels like progress tracker (left) and info panel (right).
    /// <para>
    /// Design reference: Master Plan §7.1 — "Panel trượt từ cạnh màn hình,
    /// auto-hide sau 5 giây không tương tác."
    /// </para>
    /// </summary>
    public class SlidePanel : MonoBehaviour
    {
        /// <summary>Direction the panel slides in from.</summary>
        public enum SlideDirection { Left, Right, Top, Bottom }

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Direction the panel slides in from.")]
        private SlideDirection _direction = SlideDirection.Left;

        [SerializeField]
        [Tooltip("How far off-screen the panel starts (in pixels).")]
        private float _slideDistance = 400f;

        [SerializeField]
        [Tooltip("Duration of the slide animation in seconds.")]
        private float _slideDuration = 0.35f;

        [SerializeField]
        [Tooltip("Auto-hide the panel after this many seconds of inactivity. Set 0 to disable.")]
        private float _autoHideDelay = 5f;

        [SerializeField]
        [Tooltip("CanvasGroup for fade effect during slide.")]
        private CanvasGroup _canvasGroup;

        private RectTransform _rectTransform;
        private Vector2 _shownPosition;
        private Vector2 _hiddenPosition;
        private bool _isVisible;
        private float _lastInteractionTime;
        private CancellationTokenSource _slideCts;
        private CancellationTokenSource _autoHideCts;

        /// <summary>Whether the panel is currently visible.</summary>
        public bool IsVisible => _isVisible;

        /// <summary>Fired when the panel finishes showing.</summary>
        public event Action OnPanelShown;

        /// <summary>Fired when the panel finishes hiding.</summary>
        public event Action OnPanelHidden;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                Debug.LogError("[SlidePanel] Missing RectTransform!");
                return;
            }

            _shownPosition = _rectTransform.anchoredPosition;
            _hiddenPosition = CalculateHiddenPosition();

            // Start hidden.
            _rectTransform.anchoredPosition = _hiddenPosition;
            _isVisible = false;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// Calculates the off-screen position based on slide direction.
        /// </summary>
        private Vector2 CalculateHiddenPosition()
        {
            Vector2 offset = _direction switch
            {
                SlideDirection.Left => new Vector2(-_slideDistance, 0),
                SlideDirection.Right => new Vector2(_slideDistance, 0),
                SlideDirection.Top => new Vector2(0, _slideDistance),
                SlideDirection.Bottom => new Vector2(0, -_slideDistance),
                _ => Vector2.zero
            };

            return _shownPosition + offset;
        }

        /// <summary>
        /// Shows the panel with slide animation. Resets auto-hide timer.
        /// </summary>
        public void Show()
        {
            if (_isVisible) return;

            CancelSlide();
            _slideCts = new CancellationTokenSource();
            SlideToPosition(_hiddenPosition, _shownPosition, true, _slideCts.Token).Forget();

            ResetAutoHideTimer();
        }

        /// <summary>
        /// Hides the panel with slide animation.
        /// </summary>
        public void Hide()
        {
            if (!_isVisible) return;

            CancelSlide();
            CancelAutoHide();
            _slideCts = new CancellationTokenSource();
            SlideToPosition(_shownPosition, _hiddenPosition, false, _slideCts.Token).Forget();
        }

        /// <summary>
        /// Toggles the panel visibility.
        /// </summary>
        public void Toggle()
        {
            if (_isVisible) Hide();
            else Show();
        }

        /// <summary>
        /// Call this when the user interacts with the panel to reset auto-hide timer.
        /// </summary>
        public void NotifyInteraction()
        {
            _lastInteractionTime = Time.time;
            ResetAutoHideTimer();
        }

        /// <summary>
        /// Animates the panel between two positions with easing.
        /// </summary>
        private async UniTaskVoid SlideToPosition(Vector2 from, Vector2 to,
            bool showing, CancellationToken ct)
        {
            if (_rectTransform == null) return;

            float elapsed = 0f;

            while (elapsed < _slideDuration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _slideDuration);

                // Ease out cubic for smooth deceleration.
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                _rectTransform.anchoredPosition = Vector2.Lerp(from, to, eased);

                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = showing ? eased : (1f - eased);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            _rectTransform.anchoredPosition = to;
            _isVisible = showing;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = showing ? 1f : 0f;
                _canvasGroup.interactable = showing;
                _canvasGroup.blocksRaycasts = showing;
            }

            if (showing) OnPanelShown?.Invoke();
            else OnPanelHidden?.Invoke();
        }

        /// <summary>
        /// Resets and starts the auto-hide countdown.
        /// </summary>
        private void ResetAutoHideTimer()
        {
            if (_autoHideDelay <= 0f) return;

            CancelAutoHide();
            _autoHideCts = new CancellationTokenSource();
            AutoHideCountdown(_autoHideCts.Token).Forget();
        }

        /// <summary>
        /// Waits for the auto-hide delay, then hides the panel if still visible.
        /// </summary>
        private async UniTaskVoid AutoHideCountdown(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_autoHideDelay),
                    ignoreTimeScale: false,
                    PlayerLoopTiming.Update,
                    ct
                );

                if (_isVisible)
                {
                    Hide();
                }
            }
            catch (OperationCanceledException) { }
        }

        private void CancelSlide()
        {
            if (_slideCts != null)
            {
                _slideCts.Cancel();
                _slideCts.Dispose();
                _slideCts = null;
            }
        }

        private void CancelAutoHide()
        {
            if (_autoHideCts != null)
            {
                _autoHideCts.Cancel();
                _autoHideCts.Dispose();
                _autoHideCts = null;
            }
        }

        private void OnDestroy()
        {
            CancelSlide();
            CancelAutoHide();
        }
    }
}
