using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace MotionAR.UI
{
    /// <summary>
    /// Hover-to-click button that activates after the cursor hovers over it
    /// for a configurable duration. Shows a radial loading indicator.
    /// <para>
    /// Design reference: Master Plan §7.2 — "Loading ring 0.3s, kích hoạt + beep."
    /// This replaces traditional click buttons for hand-tracking interaction.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HoverButton : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        [Tooltip("Time in seconds the cursor must hover before activation.")]
        private float _hoverDuration = 0.3f;

        [SerializeField]
        [Tooltip("Audio clip played on activation.")]
        private AudioClip _activateSound;

        [Header("Visual")]
        [SerializeField]
        [Tooltip("Image used as the radial loading indicator (set Image Type to Filled).")]
        private Image _loadingRing;

        [SerializeField]
        [Tooltip("Image for the button icon or label background.")]
        private Image _buttonBackground;

        [SerializeField]
        [Tooltip("Color of the loading ring while hovering.")]
        private Color _hoverColor = new Color(0f, 1f, 1f, 0.8f); // Cyan

        [SerializeField]
        [Tooltip("Color of the ring on successful activation.")]
        private Color _activateColor = new Color(0.22f, 1f, 0.08f, 1f); // Neon green

        [SerializeField]
        [Tooltip("Scale multiplier when hovering.")]
        private float _hoverScale = 1.1f;

        /// <summary>Fired when the hover duration is completed and the button activates.</summary>
        public event Action OnActivated;

        private RectTransform _rectTransform;
        private Vector3 _originalScale;
        private float _hoverTimer;
        private bool _isHovering;
        private bool _isActivated;
        private bool _isCooldown;

        // Cooldown to prevent immediate re-activation.
        private const float COOLDOWN_DURATION = 0.5f;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _originalScale = _rectTransform.localScale;

            if (_loadingRing != null)
            {
                _loadingRing.fillAmount = 0f;
                _loadingRing.color = _hoverColor;
            }
        }

        /// <summary>
        /// Call this each frame from the cursor/hand tracking system to check
        /// if the cursor is over this button.
        /// </summary>
        /// <param name="cursorScreenPos">Cursor position in screen coordinates.</param>
        /// <param name="camera">Camera used for screen-to-world projection.</param>
        public void UpdateHover(Vector2 cursorScreenPos, Camera camera)
        {
            if (_isCooldown) return;

            bool isOver = RectTransformUtility.RectangleContainsScreenPoint(
                _rectTransform, cursorScreenPos, camera);

            if (isOver && !_isHovering)
            {
                StartHover();
            }
            else if (!isOver && _isHovering)
            {
                CancelHover();
            }

            if (_isHovering && !_isActivated)
            {
                _hoverTimer += Time.deltaTime;

                // Update loading ring.
                float progress = Mathf.Clamp01(_hoverTimer / _hoverDuration);
                if (_loadingRing != null)
                {
                    _loadingRing.fillAmount = progress;
                }

                // Scale up slightly.
                float scale = Mathf.Lerp(1f, _hoverScale, progress);
                _rectTransform.localScale = _originalScale * scale;

                if (progress >= 1f)
                {
                    Activate();
                }
            }
        }

        /// <summary>
        /// Begins the hover countdown.
        /// </summary>
        private void StartHover()
        {
            _isHovering = true;
            _hoverTimer = 0f;
            _isActivated = false;

            if (_loadingRing != null)
            {
                _loadingRing.color = _hoverColor;
                _loadingRing.fillAmount = 0f;
            }
        }

        /// <summary>
        /// Cancels the hover and resets visuals.
        /// </summary>
        private void CancelHover()
        {
            _isHovering = false;
            _hoverTimer = 0f;

            if (_loadingRing != null)
            {
                _loadingRing.fillAmount = 0f;
            }

            _rectTransform.localScale = _originalScale;
        }

        /// <summary>
        /// Activates the button after the full hover duration.
        /// </summary>
        private void Activate()
        {
            _isActivated = true;
            _isHovering = false;

            // Flash activation color.
            if (_loadingRing != null)
            {
                _loadingRing.color = _activateColor;
                _loadingRing.fillAmount = 1f;
            }

            // Play sound.
            if (_activateSound != null)
            {
                AudioSource.PlayClipAtPoint(_activateSound, Camera.main.transform.position);
            }

            // Fire event.
            OnActivated?.Invoke();

            // Start cooldown.
            _isCooldown = true;
            CooldownAsync().Forget();
        }

        /// <summary>
        /// Cooldown period after activation to prevent instant re-trigger.
        /// </summary>
        private async UniTaskVoid CooldownAsync()
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(COOLDOWN_DURATION),
                ignoreTimeScale: false,
                PlayerLoopTiming.Update,
                destroyCancellationToken
            );

            _isCooldown = false;
            _isActivated = false;

            if (_loadingRing != null)
            {
                _loadingRing.fillAmount = 0f;
                _loadingRing.color = _hoverColor;
            }

            _rectTransform.localScale = _originalScale;
        }

        /// <summary>
        /// Resets the button to its initial state.
        /// </summary>
        public void ResetButton()
        {
            _isHovering = false;
            _isActivated = false;
            _isCooldown = false;
            _hoverTimer = 0f;

            if (_loadingRing != null)
            {
                _loadingRing.fillAmount = 0f;
                _loadingRing.color = _hoverColor;
            }

            _rectTransform.localScale = _originalScale;
        }
    }
}
